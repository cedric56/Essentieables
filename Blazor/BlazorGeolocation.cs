

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Geolocation backed by navigator.geolocation (permission-prompted by the browser).</summary>
internal sealed class BlazorGeolocation : IGeolocation
{
    public sealed record Payload(double Latitude, double Longitude, double? Accuracy,
        double? Altitude, double? AltitudeAccuracy, double? Heading, double? Speed, double Timestamp);

    private readonly object _gate = new();
    private DotNetObjectReference<BlazorGeolocation>? _ref;
    private Location? _lastKnownLocation;
    private int _watchId = -1;
    private bool _isEnabled = true;

    public bool IsListeningForeground
    {
        get { lock (_gate) return _watchId >= 0; }
    }

    /// <summary>
    /// False when the browser has no geolocation API, the page is not a secure context (HTTPS) or the
    /// permission is denied. Read at initialization and refreshed after each request.
    /// </summary>
    public bool IsEnabled => _isEnabled;

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;
    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;

    internal async Task InitializeAsync()
    {
        try
        {
            _isEnabled = await BlazorEssentials.Module.InvokeAsync<bool>("geoIsEnabled").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public Task<Location?> GetLastKnownLocationAsync() => Task.FromResult(_lastKnownLocation);

    public async Task<Location?> GetLocationAsync(Microsoft.Maui.Devices.Sensors.GeolocationRequest request, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancelToken.ThrowIfCancellationRequested();
        try
        {
            var payload = await BlazorEssentials.Module.InvokeAsync<Payload?>(
                "geoGetCurrentPosition", cancelToken, UseHighAccuracy(request), request.Timeout.TotalMilliseconds).ConfigureAwait(false);
            _isEnabled = true;
            return payload is null ? null : _lastKnownLocation = ToLocation(payload);
        }
        catch (JSException ex) when (IsError(ex, "permission"))
        {
            _isEnabled = false;
            throw new PermissionException("Geolocation permission was denied by the user or browser policy.");
        }
        catch (JSException ex) when (IsError(ex, "unsupported"))
        {
            _isEnabled = false;
            throw new FeatureNotSupportedException("Geolocation is not supported by this browser.");
        }
        catch (JSException ex) when (IsError(ex, "insecure"))
        {
            _isEnabled = false;
            throw new FeatureNotSupportedException("Geolocation requires a secure context (HTTPS).");
        }
        catch (JSException ex) when (IsError(ex, "unavailable"))
        {
            throw new FeatureNotEnabledException("The position is unavailable (location services off or no signal).");
        }
        catch (JSException ex) when (IsError(ex, "timeout"))
        {
            return null; // matches MAUI's behavior of returning null on timeout
        }
        // Other failures (disconnected circuit, cancellation, unexpected JS errors) reach the caller.
    }

    static bool IsError(JSException ex, string name) =>
        ex.Message.Contains(name, StringComparison.OrdinalIgnoreCase);

    public async Task<bool> StartListeningForegroundAsync(Microsoft.Maui.Devices.Sensors.GeolocationListeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        DotNetObjectReference<BlazorGeolocation> reference;
        lock (_gate)
        {
            if (_watchId >= 0 || _ref is not null)
                throw new InvalidOperationException("Already listening for location changes.");
            // Reserved before the await, so two concurrent starts cannot both succeed.
            _ref = reference = DotNetObjectReference.Create(this);
        }

        var highAccuracy = request.DesiredAccuracy is Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Best or Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.High;

        var watchId = -1;
        try
        {
            watchId = await BlazorEssentials.Module.InvokeAsync<int>("geoWatchStart", reference, highAccuracy).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        lock (_gate)
        {
            if (watchId >= 0 && ReferenceEquals(_ref, reference))
            {
                _watchId = watchId;
                return true;
            }
            if (ReferenceEquals(_ref, reference))
                _ref = null;
        }

        // Failed, or stopped while starting: release what was created.
        reference.Dispose();
        if (watchId >= 0)
            _ = StopWatchAsync(watchId, null);
        return false;
    }

    /// <summary>
    /// Stops listening at once (<see cref="IsListeningForeground"/> is false on return, and a new
    /// listener may be started right away); the browser watch is cleared in the background.
    /// </summary>
    public void StopListeningForeground()
    {
        int watchId;
        DotNetObjectReference<BlazorGeolocation>? reference;
        lock (_gate)
        {
            watchId = _watchId;
            reference = _ref;
            _watchId = -1;
            _ref = null;
        }

        if (watchId >= 0)
            _ = StopWatchAsync(watchId, reference);
        else
            reference?.Dispose();
    }

    private static async Task StopWatchAsync(int watchId, DotNetObjectReference<BlazorGeolocation>? reference)
    {
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("geoWatchStop", watchId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            // Disposed only once the watch is cleared, so a late callback does not hit a disposed reference.
            reference?.Dispose();
        }
    }

    [JSInvokable]
    public void OnGeolocationChanged(Payload payload)
    {
        var location = ToLocation(payload);
        _lastKnownLocation = location;
        LocationChanged?.Invoke(this, new GeolocationLocationChangedEventArgs(location));
    }

    [JSInvokable]
    public void OnGeolocationError(string errorCode)
    {
        var error = errorCode switch
        {
            "permission" => GeolocationError.Unauthorized,
            _ => GeolocationError.PositionUnavailable,
        };
        if (error == GeolocationError.Unauthorized)
            _isEnabled = false;
        ListeningFailed?.Invoke(this, new GeolocationListeningFailedEventArgs(error));
    }

    private static bool UseHighAccuracy(Microsoft.Maui.Devices.Sensors.GeolocationRequest request) =>
        request.DesiredAccuracy is Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Best or Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.High;

    private static Location ToLocation(Payload p) => new(p.Latitude, p.Longitude)
    {
        Accuracy = p.Accuracy,
        Altitude = p.Altitude,
        VerticalAccuracy = p.AltitudeAccuracy,
        Course = p.Heading,
        Speed = p.Speed,
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)p.Timestamp),
    };
}