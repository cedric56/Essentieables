using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Geolocation backed by navigator.geolocation (permission-prompted by the browser).</summary>
public sealed class BlazorGeolocation : IGeolocation
{
    public sealed record Payload(double Latitude, double Longitude, double? Accuracy,
        double? Altitude, double? AltitudeAccuracy, double? Heading, double? Speed, double Timestamp);

    private DotNetObjectReference<BlazorGeolocation>? _ref;
    private Location? _lastKnownLocation;
    private int _watchId = -1;

    public bool IsListeningForeground => _watchId >= 0;

    // Browsers only reveal permission state at request time; assume available.
    public bool IsEnabled => true;

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;
    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;

    public Task<Location?> GetLastKnownLocationAsync() => Task.FromResult(_lastKnownLocation);

    public async Task<Location?> GetLocationAsync(Microsoft.Maui.Devices.Sensors.GeolocationRequest request, CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        try
        {
            var payload = await BlazorEssentials.Module.InvokeAsync<Payload>("geoGetCurrentPosition", cancelToken, UseHighAccuracy(request), request.Timeout.TotalMilliseconds);
            if (payload != null)
                return _lastKnownLocation = ToLocation(payload);
        }
        catch (JSException ex) when (ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            throw new PermissionException("Geolocation permission was denied by the user or browser policy.");
        }
        catch (JSException ex) when (ex.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotSupportedException("Geolocation is not supported by this browser.");
        }
        catch (JSException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return null; // matches MAUI's behavior of returning null on timeout
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
        return null;
    }

    public async Task<bool> StartListeningForegroundAsync(Microsoft.Maui.Devices.Sensors.GeolocationListeningRequest request)
    {
        if (IsListeningForeground)
            throw new InvalidOperationException("Already listening for location changes.");

        _ref = DotNetObjectReference.Create(this);
        var highAccuracy = request.DesiredAccuracy is Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Best or Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.High;

        try
        {
            _watchId = await BlazorEssentials.Module.InvokeAsync<int>("geoWatchStart", _ref, highAccuracy);
        }
        catch (JSDisconnectedException)
        {
            _watchId = -1;
        }       
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            _watchId = -1;
        }
#else
        catch
        {
            _watchId = -1;
        }
#endif

        return _watchId >= 0;
    }

    public void StopListeningForeground()
    {
        _ = StopListeningForegroundAsync();
    }

    private async Task StopListeningForegroundAsync()
    {
        try
        {
            if (_watchId >= 0)
            {
                await BlazorEssentials.Module.InvokeVoidAsync("geoWatchStop", _watchId);
                _watchId = -1;
            }
        }
#if DEBUG
        catch (Exception ex)
        {
            _watchId = -1;
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {
            _watchId = -1;
        }
#endif
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
            "timeout" => GeolocationError.PositionUnavailable,
            _ => GeolocationError.PositionUnavailable,
        };
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