using Microsoft.JSInterop;
using Microsoft.Maui.Devices;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Display info from window.innerWidth/Height and devicePixelRatio. KeepScreenOn uses the
/// Screen Wake Lock API where available (secure contexts, most modern browsers), re-acquired
/// automatically when the page becomes visible again, since browsers release wake locks
/// whenever the tab is hidden.
/// </summary>
public sealed class BlazorDeviceDisplay : IDeviceDisplay, IAsyncDisposable
{
    public sealed record Snapshot(double Width, double Height, double PixelRatio, string OrientationType, bool WakeLockActive);
    public sealed record ChangeSnapshot(double Width, double Height, double PixelRatio, string OrientationType);
    private readonly SemaphoreSlim _watchingLock = new(1, 1);
    private DotNetObjectReference<BlazorDeviceDisplay>? _ref;
    private bool _watching;

    private DisplayInfo _display = new(0, 0, 1, DisplayOrientation.Unknown, DisplayRotation.Unknown);
    private bool _wakeLockActive;

    private event EventHandler<DisplayInfoChangedEventArgs>? _mainDisplayInfoChanged;

    public async Task InitializeAsync()
    {
        try
        { 
            var snapshot = await BlazorEssentials.Module.InvokeAsync<Snapshot?>("ddGetSnapshot");
            if (snapshot is not null)
            {
                Apply(snapshot.Width, snapshot.Height, snapshot.PixelRatio, snapshot.OrientationType);
                _wakeLockActive = snapshot.WakeLockActive;
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

    // ───────────────────────── IDeviceDisplay ─────────────────────────

    public bool KeepScreenOn
    {
        get => _wakeLockActive;
        set => _ = SetWakeLockSafelyAsync(value);
    }

    private async Task SetWakeLockSafelyAsync(bool on)
    {
        try
        {
            await EnsureWatchingAsync().ConfigureAwait(false);

            var reference = _ref
                ?? throw new InvalidOperationException(
                    "The display callback reference was not initialized.");

            await BlazorEssentials.Module.InvokeVoidAsync(
                "ddSetWakeLock", reference, on).ConfigureAwait(false);
        }

#if DEBUG
catch (Exception ex)
{
Debug.WriteLine(ex);
}
#else
        catch
        {
            // The MAUI property setter is synchronous and cannot propagate
            // asynchronous JavaScript interop failures to its caller.
        }
#endif
    }

    public DisplayInfo MainDisplayInfo => _display;

    public event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChanged
    {
        add { _ = EnsureWatchingAsync(); _mainDisplayInfoChanged += value; }
        remove => _mainDisplayInfoChanged -= value;
    }

    // ───────────────────────── internals ─────────────────────────
    //private Task? _ensureWatchingTask;

    private Task EnsureWatchingAsync()
    {
        return EnsureWatchingCoreAsync();
    }

    private async Task EnsureWatchingCoreAsync()
    {
        await _watchingLock.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_watching)
                return;

            _ref ??= DotNetObjectReference.Create(this);

            try
            {
                await BlazorEssentials.Module.InvokeVoidAsync(
                    "ddSubscribe", _ref).ConfigureAwait(false);

                // Only report success after the subscription completes.
                _watching = true;
            }
            catch
            {
                // A failed subscription must be retryable.
                _watching = false;
                _ref.Dispose();
                _ref = null;
                throw;
            }
        }
        finally
        {
            _watchingLock.Release();
        }

    }

    private async Task SetWakeLockAsync(bool on)
    {
        await EnsureWatchingAsync();
        try
        { 
            await BlazorEssentials.Module.InvokeVoidAsync("ddSetWakeLock", _ref, on);
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

    [JSInvokable]
    public void OnWakeLockChanged(bool active) => _wakeLockActive = active;

    [JSInvokable]
    public void OnDisplayChanged(ChangeSnapshot s)
    {
        Apply(s.Width, s.Height, s.PixelRatio, s.OrientationType);
        _mainDisplayInfoChanged?.Invoke(this, new DisplayInfoChangedEventArgs(_display));
    }

    private void Apply(double width, double height, double pixelRatio, string orientationType)
    {
        var orientation = orientationType.StartsWith("portrait", StringComparison.Ordinal)
            ? DisplayOrientation.Portrait
            : DisplayOrientation.Landscape;

        var rotation = orientationType switch
        {
            "portrait-primary" or "landscape-primary" => DisplayRotation.Rotation0,
            "landscape-secondary" or "portrait-secondary" => DisplayRotation.Rotation180,
            _ => DisplayRotation.Unknown,
        };

        _display = new DisplayInfo(width, height, pixelRatio, orientation, rotation);
    }

    public async ValueTask DisposeAsync()
    {
        try
        { 
            await BlazorEssentials.Module.InvokeVoidAsync("ddUnsubscribe");
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
        _ref?.Dispose();
        _ref = null;
    }
}