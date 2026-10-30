// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// <see cref="IGeocoding"/> backed by the public OpenStreetMap Nominatim service.
/// Shared by the Avalonia and Blazor hosts.
/// </summary>
/// <remarks>
/// The public Nominatim instance is limited to about one request per second and requires an
/// identifying User-Agent: see https://operations.osmfoundation.org/policies/nominatim/.
/// Browsers ignore the User-Agent header set here and send their own.
/// </remarks>
public class NominatimGeocoding : IGeocoding
{
    /// <summary>Base address of the public Nominatim instance.</summary>
    public const string DefaultBaseAddress = "https://nominatim.openstreetmap.org/";

    static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Essentieables/1.0");
        return client;
    });

    readonly HttpClient _client;
    readonly string _baseAddress;

    /// <summary>Creates a geocoder that calls <paramref name="baseAddress"/> with <paramref name="client"/>.</summary>
    public NominatimGeocoding(HttpClient? client = null, string baseAddress = DefaultBaseAddress)
    {
        _client = client ?? SharedClient.Value;
        _baseAddress = baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/";
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Placemark>> GetPlacemarksAsync(double latitude, double longitude)
    {
        var json = await _client.GetStringAsync(BuildReverseUrl(latitude, longitude, _baseAddress)).ConfigureAwait(false);
        return ParsePlacemarks(json);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Location>> GetLocationsAsync(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var json = await _client.GetStringAsync(BuildSearchUrl(address, _baseAddress)).ConfigureAwait(false);
        return ParseLocations(json);
    }

    /// <summary>Builds the reverse-geocoding URL. Coordinates are always formatted with the invariant culture.</summary>
    public static string BuildReverseUrl(double latitude, double longitude, string baseAddress = DefaultBaseAddress) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{baseAddress}reverse?format=json&lat={latitude:R}&lon={longitude:R}&addressdetails=1");

    /// <summary>Builds the forward-geocoding URL.</summary>
    public static string BuildSearchUrl(string address, string baseAddress = DefaultBaseAddress) =>
        $"{baseAddress}search?format=json&q={Uri.EscapeDataString(address)}";

    /// <summary>Converts a Nominatim <c>/reverse</c> JSON response to placemarks.</summary>
    public static IReadOnlyList<Placemark> ParsePlacemarks(string json)
    {
        var data = JsonSerializer.Deserialize(json, AvaeJsonSerializerContext.Default.PlacemarkResponseInterop);
        if (data?.Lat is null || data.Lon is null)
            return [];

        var address = data.Address;
        return
        [
            new Placemark
            {
                CountryName = address?.Country ?? string.Empty,
                CountryCode = address?.CcountryCode?.ToUpperInvariant() ?? string.Empty,
                Location = new Location(data.LatValue, data.LonValue),
                FeatureName = address?.Road ?? data.Name ?? string.Empty,
                Locality = address?.City ?? address?.Town ?? address?.Village ?? address?.Hamlet ?? string.Empty,
                AdminArea = address?.State ?? string.Empty,
                SubAdminArea = address?.County ?? string.Empty,
                PostalCode = address?.Postcode ?? string.Empty,
            }
        ];
    }

    /// <summary>Converts a Nominatim <c>/search</c> JSON response to locations.</summary>
    public static IReadOnlyList<Location> ParseLocations(string json)
    {
        var data = JsonSerializer.Deserialize(json, AvaeJsonSerializerContext.Default.IEnumerableNominatimResponse);
        return data?.Select(d => new Location(d.LatValue, d.LonValue)).ToList() ?? [];
    }
}
