using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Maui.Storage;

namespace Essentieables;

/// <summary>
/// Preferences backed by window.localStorage. All values are loaded into memory once via
/// <see cref="InitializeAsync"/>, so the synchronous <see cref="IPreferences"/> surface never
/// calls JS interop directly for reads. Writes update the in-memory cache immediately (so
/// Get always reflects the latest Set/Remove within this session) and persist to localStorage
/// in the background. A per-key semaphore serializes those background persists, so a rapid
/// Set-then-Remove (or vice versa) on the same key can't land in localStorage out of order.
/// </summary>
public sealed class BlazorPreferences : IPreferences
{
    private readonly object _pendingLock = new();
    private readonly Dictionary<long, Task> _pending = new();
    private readonly ConcurrentQueue<Exception> _persistenceErrors = new();
    private long _nextOperationId;


    private const string KeyPrefix = "maui:prefs:";

    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private static string GetContainer(string? sharedName) => KeyPrefix + (sharedName ?? "_default") + ":";
    private static string GetStorageKey(string key, string? sharedName) => GetContainer(sharedName) + key;

    private SemaphoreSlim GetLock(string storageKey)
        => _locks.GetOrAdd(storageKey, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// Loads all existing preference entries from localStorage into memory. Idempotent.
    /// Must complete before any Get/Set/Remove/Clear/ContainsKey call.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var all = await BlazorEssentials.Module.InvokeAsync<Dictionary<string, string>>("prefsGetAll", KeyPrefix)
                .ConfigureAwait(false);

            foreach (var (k, v) in all ?? [])
                _cache[k] = v;
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif
    }

    public bool ContainsKey(string key, string? sharedName = null)
        => _cache.ContainsKey(GetStorageKey(key, sharedName));

    public void Remove(string key, string? sharedName = null)
    {
        var storageKey = GetStorageKey(key, sharedName);
        if (_cache.TryRemove(storageKey, out _))
            TrackPersistence(() => PersistRemoveAsync(storageKey));
    }

    public void Clear(string? sharedName = null)
    {
        var prefix = GetContainer(sharedName);
        foreach (var storageKey in _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            if (_cache.TryRemove(storageKey, out _))
                TrackPersistence(() => PersistRemoveAsync(storageKey));
        }
    }

    public void Set<T>(string key, T value, string? sharedName = null)
    {
        var storageKey = GetStorageKey(key, sharedName);

        if (value is null)
        {
            if (_cache.TryRemove(storageKey, out _))
                TrackPersistence(() => PersistRemoveAsync(storageKey));
            return;
        }

        var stored = value switch
        {
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IConvertible c => c.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        _cache[storageKey] = stored;
        TrackPersistence(() => PersistSetAsync(storageKey, stored));
    }

    public T Get<T>(string key, T defaultValue, string? sharedName = null)
    {
        if (!_cache.TryGetValue(GetStorageKey(key, sharedName), out var stored))
            return defaultValue;

        try
        {
            var type = typeof(T);
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(DateTime))
                return (T)(object)DateTime.Parse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (underlying == typeof(DateTimeOffset))
                return (T)(object)DateTimeOffset.Parse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return (T)Convert.ChangeType(stored, underlying, CultureInfo.InvariantCulture);
        }
        catch
        {
            return defaultValue;
        }
    }

    // ── Persistence: fire-and-forget, but serialized per key ──────────────

    private async Task PersistSetAsync(string storageKey, string value)
    {
        var gate = GetLock(storageKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("prefsSet", storageKey, value)
                .ConfigureAwait(false);
        }        
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif
        finally
        {
            gate.Release();
        }
    }

    private async Task PersistRemoveAsync(string storageKey)
    {
        var gate = GetLock(storageKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("prefsRemove", storageKey)
                .ConfigureAwait(false);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif
        finally
        {
            gate.Release();
        }
    }

    private void TrackPersistence(Func<Task> operation)
    {
        lock (_pendingLock)
        {
            var id = ++_nextOperationId;
            var task = ExecutePersistenceAsync(operation);

            _pending.Add(id, task);

            _ = task.ContinueWith(
                _ =>
                {
                    lock (_pendingLock)
                        _pending.Remove(id);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task ExecutePersistenceAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Includes JSDisconnectedException. The failure is observed
            // here and reported to callers through FlushAsync().
            _persistenceErrors.Enqueue(ex);
        }
    }

    /// <summary>
    /// Waits for persistence operations pending when this method is called.
    /// Throws if any persistence operations have failed since the last
    /// successful error-draining call.
    /// </summary>
    public async Task FlushAsync()
    {
        Task[] pending;

        lock (_pendingLock)
            pending = _pending.Values.ToArray();

        await Task.WhenAll(pending).ConfigureAwait(false);

        var errors = new List<Exception>();

        while (_persistenceErrors.TryDequeue(out var error))
            errors.Add(error);

        if (errors.Count > 0)
            throw new AggregateException(
                "One or more browser preference persistence operations failed.",
                errors);
    }
}