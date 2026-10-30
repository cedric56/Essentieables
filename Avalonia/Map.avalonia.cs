using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Globalization;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.System;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
class OriginalMapImplementation : IMap
{
    public Task OpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        var uri = GetMapsUri(latitude, longitude, options);

        return LaunchUri(uri);
    }

    public Task OpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        var uri = GetMapsUri(placemark, options);

        return LaunchUri(uri);
    }

    public async Task<bool> TryOpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        var uri = GetMapsUri(latitude, longitude, options);

        return await TryLaunchUri(uri);
    }

    public async Task<bool> TryOpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        var uri = GetMapsUri(placemark, options);

        return await TryLaunchUri(uri);
    }

    Uri GetMapsUri(double latitude, double longitude, MapLaunchOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lng = longitude.ToString(CultureInfo.InvariantCulture);
        var name = options.Name ?? string.Empty;
        var uri = string.Empty;

        if (options.NavigationMode == NavigationMode.None)
        {
            uri = $"bingmaps:?collection=point.{lat}_{lng}_{name}";
        }
        else
        {
            uri = $"bingmaps:?rtp=~pos.{lat}_{lng}_{name}{GetMode(options.NavigationMode)}";
        }

        return new Uri(uri);
    }

    Uri GetMapsUri(Placemark placemark, MapLaunchOptions options)
    {
        if (placemark == null)
            throw new ArgumentNullException(nameof(placemark));

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var uri = string.Empty;

        if (options.NavigationMode == NavigationMode.None)
        {
            uri = $"bingmaps:?where={placemark.GetEscapedAddress()}";
        }
        else
        {
            uri = $"bingmaps:?rtp=~adr.{placemark.GetEscapedAddress()}{GetMode(options.NavigationMode)}";
        }

        return new Uri(uri);
    }

    string GetMode(NavigationMode mode)
    {
        switch (mode)
        {
            case NavigationMode.Driving:
                return "&mode=d";
            case NavigationMode.Transit:
                return "&mode=t";
            case NavigationMode.Walking:
                return "&mode=w";
        }
        return string.Empty;
    }

    async Task<bool> TryLaunchUri(Uri uri)
    {
        var canLaunch = await CanLaunchUri(uri);

        if (canLaunch)
        {
            await LaunchUri(uri);
        }

        return canLaunch;
    }

    Task LaunchUri(Uri mapsUri) =>
        global::Windows.System.Launcher.LaunchUriAsync(mapsUri).AsTask();

    async Task<bool> CanLaunchUri(Uri uri)
    {
        var supported = await global::Windows.System.Launcher.QueryUriSupportAsync(uri, LaunchQuerySupportType.Uri);
        return supported == LaunchQuerySupportStatus.Available;
    }
}

/// <summary>
/// This class contains static extension methods for use with <see cref="Placemark"/> objects.
/// </summary>
internal static partial class OriginalPlacemarkExtensions
{
    /// <inheritdoc cref="Map.OpenAsync(Placemark, MapLaunchOptions)"/>
    public static Task OpenMapsAsync(this Placemark placemark, MapLaunchOptions options) =>
        Map.OpenAsync(placemark, options);

    /// <inheritdoc cref="Map.OpenAsync(Placemark)"/>
    public static Task OpenMapsAsync(this Placemark placemark) =>
        Map.OpenAsync(placemark);

    internal static string GetEscapedAddress(this Placemark placemark)
    {
        if (placemark == null)
            throw new ArgumentNullException(nameof(placemark));

        var address = $"{placemark.Thoroughfare} {placemark.Locality} {placemark.AdminArea} {placemark.PostalCode} {placemark.CountryName}";

        return Uri.EscapeDataString(address);
    }
}
