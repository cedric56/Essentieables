

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
internal sealed class BlazorPreferences : IPreferences
{
    private readonly object _pendingLock = new();
    private readonly Dictionary<long, Task> _pending = new();
    private readonly ConcurrentQueue<Exception> _persistenceErrors = new();
    private long _nextOperationId;


    internal const string KeyPrefix = "maui:prefs:";

    private readonly ConcurrentDictionary<string, string> _cache = new();

    // Keys written or removed before InitializeAsync finished loading: the loaded values must not
    // overwrite them.
    private readonly ConcurrentDictionary<string, byte> _touchedBeforeLoad = new();
    private readonly ConcurrentDictionary<string, byte> _clearedBeforeLoad = new();
    private volatile bool _loaded;

    /// <summary>True once the values stored in localStorage have been loaded.</summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Reads the localStorage entries starting with a prefix synchronously, or returns null when it cannot. Given in
    /// Avalonia.Browser apps, where [JSImport] is synchronous: the values are then loaded on first use, before
    /// <see cref="InitializeAsync"/> completes, so startup code reads the stored preferences.
    /// </summary>
    private readonly Func<string, Dictionary<string, string>?>? _synchronousLoader;

    private readonly object _loadLock = new();

    public BlazorPreferences()
    {
    }

    internal BlazorPreferences(Func<string, Dictionary<string, string>?>? synchronousLoader)
    {
        _synchronousLoader = synchronousLoader;
    }

    private void EnsureLoaded()
    {
        if (_loaded || _synchronousLoader is not { } loader)
            return;
        lock (_loadLock)
        {
            if (_loaded)
                return;
            if (loader(KeyPrefix) is { } all)
                Load(all);
        }
    }

    private void Load(Dictionary<string, string> all)
    {
        foreach (var (k, v) in all)
        {
            if (!_touchedBeforeLoad.ContainsKey(k)
                && !_clearedBeforeLoad.Keys.Any(prefix => k.StartsWith(prefix, StringComparison.Ordinal)))
                _cache.TryAdd(k, v);
        }
        _loaded = true;
        _touchedBeforeLoad.Clear();
        _clearedBeforeLoad.Clear();
    }

    private void Touch(string storageKey)
    {
        if (!_loaded)
            _touchedBeforeLoad[storageKey] = 0;
    }
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    // ':' separates the container from the key: escaped in the shared name, so ("a", "b:c") and ("a:b", "c") are two keys
    // and Clear("a") leaves "a:b" alone. Names without '%' or ':' (all the existing ones in practice) are unchanged.
    private static string GetContainer(string? sharedName)
        => KeyPrefix + (sharedName is null ? "_default" : sharedName.Replace("%", "%25").Replace(":", "%3A")) + ":";
    internal static string GetStorageKey(string key, string? sharedName) => GetContainer(sharedName) + key;

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
            EnsureLoaded();
            if (_loaded)
                return;

            var all = await BlazorEssentials.Module.InvokeAsync<Dictionary<string, string>>("prefsGetAll", KeyPrefix)
                .ConfigureAwait(false);

            lock (_loadLock)
            {
                if (!_loaded)
                    Load(all ?? []);
            }
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
    {
        EnsureLoaded();
        return _cache.ContainsKey(GetStorageKey(key, sharedName));
    }

    public void Remove(string key, string? sharedName = null)
    {
        EnsureLoaded();
        var storageKey = GetStorageKey(key, sharedName);
        Touch(storageKey);
        // Before the load, the key may exist in localStorage without being in the cache yet.
        if (_cache.TryRemove(storageKey, out _) || !_loaded)
            TrackPersistence(() => PersistRemoveAsync(storageKey));
    }

    public void Clear(string? sharedName = null)
    {
        EnsureLoaded();
        var prefix = GetContainer(sharedName);
        if (!_loaded)
        {
            // Entries not loaded yet are cleared in localStorage and skipped by the load.
            _clearedBeforeLoad[prefix] = 0;
            TrackPersistence(() => PersistClearAsync(prefix));
        }
        foreach (var storageKey in _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            Touch(storageKey);
            if (_cache.TryRemove(storageKey, out _))
                TrackPersistence(() => PersistRemoveAsync(storageKey));
        }
    }

    public void Set<T>(string key, T value, string? sharedName = null)
    {
        EnsureSupported<T>();
        EnsureLoaded();
        var storageKey = GetStorageKey(key, sharedName);
        Touch(storageKey);

        if (value is null)
        {
            if (_cache.TryRemove(storageKey, out _) || !_loaded)
                TrackPersistence(() => PersistRemoveAsync(storageKey));
            return;
        }

        var stored = Serialize(value);

        _cache[storageKey] = stored;
        TrackPersistence(() => PersistSetAsync(storageKey, stored));
    }

    // The types MAUI Preferences supports; the others throw NotSupportedException there too,
    // instead of being silently turned into a string that cannot be read back.
    private static readonly Type[] SupportedTypes =
    [
        typeof(string), typeof(bool), typeof(int), typeof(long), typeof(float), typeof(double),
        typeof(DateTime), typeof(DateTimeOffset),
    ];

    internal static void EnsureSupported<T>()
    {
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (Array.IndexOf(SupportedTypes, type) < 0)
            throw new NotSupportedException($"Preferences does not support values of type {typeof(T).Name}.");
    }

    internal static string Serialize<T>(T value) => value switch
    {
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IConvertible c => c.ToString(CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException($"Preferences does not support values of type {typeof(T).Name}."),
    };

    public T Get<T>(string key, T defaultValue, string? sharedName = null)
    {
        EnsureSupported<T>();
        EnsureLoaded();
        if (!_cache.TryGetValue(GetStorageKey(key, sharedName), out var stored))
            return defaultValue;

        try
        {
            var type = typeof(T);
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(DateTime))
            {
                // A long is DateTime.ToBinary(), the format of the MAUI desktop implementations (imported preferences).
                if (long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var binary))
                    return (T)(object)DateTime.FromBinary(binary);
                return (T)(object)DateTime.Parse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
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
        finally
        {
            gate.Release();
        }
    }

    private static async Task PersistClearAsync(string prefix)
    {
        await BlazorEssentials.Module.InvokeVoidAsync("prefsClear", prefix).ConfigureAwait(false);
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