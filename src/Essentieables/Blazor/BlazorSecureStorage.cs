

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Maui.Storage;

namespace Essentieables;

/// <summary>
/// Secure storage backed by localStorage with AES-GCM encryption via WebCrypto.
/// The encryption key is non-extractable and lives in IndexedDB, so stored values
/// are unreadable outside this origin's browser context. This is best-effort:
/// any script running on the same origin can decrypt values, and clearing site
/// data destroys the key (existing values then read as null).
///
/// GetAsync/SetAsync are awaitable and call JS directly. Remove/RemoveAll are
/// synchronous per <see cref="ISecureStorage"/>: they return as soon as the
/// removal is queued, not once it's persisted. A per-key semaphore ensures any
/// GetAsync/SetAsync on the same key that starts afterward waits for the queued
/// removal to actually finish in JS first, so callers never observe a stale
/// value mid-removal.
/// </summary>
internal sealed class BlazorSecureStorage : ISecureStorage
{
    private const string KeyPrefix = "maui:securestorage:";

    private readonly ConcurrentDictionary<string, byte> _knownKeys = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private SemaphoreSlim GetLock(string storageKey)
        => _locks.GetOrAdd(storageKey, _ => new SemaphoreSlim(1, 1));

    /// <summary>Loads the set of existing keys (not values). Idempotent. Call once at startup.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var keys = await BlazorEssentials.Module.InvokeAsync<string[]>("secureKeys", KeyPrefix)
                .ConfigureAwait(false);

            foreach (var k in keys ?? [])
                _knownKeys[k] = 0;
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

    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var storageKey = KeyPrefix + key;
        var gate = GetLock(storageKey);

        // Waits for any in-flight Remove on this key to finish first, so a
        // Remove immediately followed by a Get can't return the stale value.
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // null: no value (or a value that can no longer be decrypted). Storage or crypto
            // failures throw, instead of being confused with an empty value.
            var value = await BlazorEssentials.Module.InvokeAsync<string?>("secureGet", storageKey)
                .ConfigureAwait(false);
            if (value is null)
                _knownKeys.TryRemove(storageKey, out _);
            else
                _knownKeys[storageKey] = 0;
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        var storageKey = KeyPrefix + key;
        var gate = GetLock(storageKey);

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Failures propagate: the caller must not believe an unsaved secret was stored.
            await BlazorEssentials.Module.InvokeVoidAsync("secureSet", storageKey, value)
                .ConfigureAwait(false);

            _knownKeys[storageKey] = 0;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool Remove(string key)
    {
        var storageKey = KeyPrefix + key;

        // The return value reflects what this instance knows; the removal is queued in any case,
        // so a value written by another tab is removed too.
        var known = _knownKeys.TryRemove(storageKey, out _);

        // Fire-and-forget: Remove itself stays synchronous, satisfying
        // ISecureStorage. GetAsync/SetAsync on this key will block on the same
        // gate until this finishes, so no caller can observe a stale value.
        Track(RemoveLockedAsync(GetLock(storageKey), storageKey));
        return known;
    }

    public void RemoveAll()
    {
        var keys = _knownKeys.Keys.ToList();
        foreach (var storageKey in keys)
            _knownKeys.TryRemove(storageKey, out _);

        Track(RemoveAllCoreAsync(keys));
    }

    private async Task RemoveAllCoreAsync(System.Collections.Generic.List<string> keys)
    {
        // Always in the same order, so two concurrent RemoveAll cannot deadlock.
        var gates = keys.Order(StringComparer.Ordinal).Select(GetLock).ToList();
        foreach (var gate in gates)
            await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Every prefixed entry, including those written by other tabs.
            await BlazorEssentials.Module.InvokeVoidAsync("secureRemoveAll", KeyPrefix).ConfigureAwait(false);
        }
        finally
        {
            foreach (var gate in gates)
                gate.Release();
        }
    }

    private static async Task RemoveLockedAsync(SemaphoreSlim gate, string storageKey)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("secureRemove", storageKey)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    // ── Background removals: failures are kept and reported by FlushAsync ──────────────

    private readonly object _pendingLock = new();
    private readonly System.Collections.Generic.HashSet<Task> _pending = [];
    private readonly ConcurrentQueue<Exception> _errors = new();

    private void Track(Task operation)
    {
        lock (_pendingLock)
            _pending.Add(operation);

        _ = operation.ContinueWith(t =>
        {
            if (t.Exception is { } ex)
                _errors.Enqueue(ex.InnerException ?? ex);
            lock (_pendingLock)
                _pending.Remove(t);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>
    /// Waits for the queued Remove/RemoveAll operations and throws if any of them failed since the
    /// last call, so a caller can make sure a secret is really gone (for example on sign-out).
    /// </summary>
    public async Task FlushAsync()
    {
        Task[] pending;
        lock (_pendingLock)
            pending = [.. _pending];

        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch
        {
            // Collected below through the continuation.
        }

        var errors = new System.Collections.Generic.List<Exception>();
        while (_errors.TryDequeue(out var error))
            errors.Add(error);
        if (errors.Count > 0)
            throw new AggregateException("One or more secure storage removals failed.", errors);
    }
}
