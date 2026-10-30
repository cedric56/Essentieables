// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

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
        var storageKey = KeyPrefix + key;
        var gate = GetLock(storageKey);

        // Waits for any in-flight Remove on this key to finish first, so a
        // Remove immediately followed by a Get can't return the stale value.
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await BlazorEssentials.Module.InvokeAsync<string?>("secureGet", storageKey)
                .ConfigureAwait(false);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return string.Empty;
        }
#else
        catch
        {
            return string.Empty;
        }
#endif
        finally
        {
            gate.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        var storageKey = KeyPrefix + key;
        var gate = GetLock(storageKey);

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("secureSet", storageKey, value)
                .ConfigureAwait(false);

            _knownKeys[storageKey] = 0;
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

    public bool Remove(string key)
    {
        var storageKey = KeyPrefix + key;

        // Only proceed if the key was actually known; matches the original
        // semantics of returning whether something was removed.
        if (!_knownKeys.TryRemove(storageKey, out _))
            return false;

        var gate = GetLock(storageKey);

        // Fire-and-forget: Remove itself stays synchronous, satisfying
        // ISecureStorage. GetAsync/SetAsync on this key will block on the same
        // gate until this finishes, so no caller can observe a stale value.
        _ = RemoveLockedAsync(gate, storageKey);
        return true;
    }

    public void RemoveAll()
    {
        foreach (var storageKey in _knownKeys.Keys.ToList())
        {
            if (!_knownKeys.TryRemove(storageKey, out _))
                continue;

            var gate = GetLock(storageKey);
            _ = RemoveLockedAsync(gate, storageKey);
        }
    }

    private async Task RemoveLockedAsync(SemaphoreSlim gate, string storageKey)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("secureRemove", storageKey)
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
}