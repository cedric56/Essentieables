//MIT License

//Copyright(c) 2025 MarketAlly Pte Ltd

//Lead Architect: David H. Friedel Jr.

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Map apps cannot be launched from the browser backend.</summary>
public class BlazorMap(BlazorBrowser browser) : IMap
{
    // Argument checks run before anything is launched and throw synchronously, as MAUI's
    // MapImplementation does (ArgumentNullException for a null placemark or options).

    public async Task OpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        var url = BuildUrl(latitude, longitude, Required(options));
        await browser.OpenAsync(url);
    }

    public async Task OpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        var url = BuildUrl(Required(placemark), Required(options));
        await browser.OpenAsync(url);
    }

    public async Task<bool> TryOpenAsync(double latitude, double longitude, MapLaunchOptions options)
    {
        var url = BuildUrl(latitude, longitude, Required(options));
        return await browser.OpenAsync(url);
    }

    public async Task<bool> TryOpenAsync(Placemark placemark, MapLaunchOptions options)
    {
        var url = BuildUrl(Required(placemark), Required(options));
        return await browser.OpenAsync(url);
    }

    private static T Required<T>(T value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
        => value ?? throw new ArgumentNullException(name);

    /// <summary>
    /// OpenStreetMap URL for a coordinate. Uses invariant formatting so the
    /// decimal separator is always '.', and switches to a directions URL when
    /// the caller asked for navigation.
    /// </summary>
    internal static string BuildUrl(double latitude, double longitude, MapLaunchOptions? options)
    {
        var lat = latitude.ToString("R", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("R", CultureInfo.InvariantCulture);

        if (options?.NavigationMode is { } mode && mode != NavigationMode.None)
        {
            var engine = mode switch
            {
                NavigationMode.Walking => "fossgis_osrm_foot",
                NavigationMode.Bicycling => "fossgis_osrm_bike",
                _ => "fossgis_osrm_car",
            };
            return $"https://www.openstreetmap.org/directions?engine={engine}&route=;{lat},{lon}";
        }

        return $"https://www.openstreetmap.org/?mlat={lat}&mlon={lon}#map=15/{lat}/{lon}";
    }

    /// <summary>
    /// OpenStreetMap search URL for a placemark: the non-empty address parts
    /// joined with single spaces and percent-encoded.
    /// </summary>
    internal static string BuildUrl(Placemark placemark, MapLaunchOptions? options)
    {
        var parts = new[]
        {
            placemark.SubThoroughfare,
            placemark.Thoroughfare,
            placemark.Locality,
            placemark.AdminArea,
            placemark.PostalCode,
            placemark.CountryName,
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim());

        var address = string.Join(" ", parts);
        if (string.IsNullOrEmpty(address) && !string.IsNullOrWhiteSpace(options?.Name))
            address = options!.Name.Trim();

        return $"https://www.openstreetmap.org/search?query={Uri.EscapeDataString(address)}";
    }
}
