using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Geocoding requires a service backend; none is available in the browser.</summary>
public class BlazorGeocoding : IGeocoding
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Essentieables/1.0");

        return client;
    }

    public async Task<IEnumerable<Placemark>> GetPlacemarksAsync(double latitude, double longitude)
    {
        var httpClient = BlazorEssentials.Provider.GetService<HttpClient>();
        if (httpClient != null)
            Client.BaseAddress = httpClient.BaseAddress;

        // Nominatim reverse geocoding URL
        string url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude}&lon={longitude}&addressdetails=1";

        // Make the request to the Nominatim API
        var response = await Client.GetStringAsync(url);

        // Parse the JSON response
        var data = JsonSerializer.Deserialize(response, AvaeJsonSerializerContext.Default.PlacemarkResponseInterop);

        if (data is null)
        {
            return [];
        }

        // Return the full address (can also return specific components)
        return new List<Placemark>()
        {
            new()
            {
                CountryName = data.Address?.Country ?? string.Empty,
                CountryCode = data.Address?.CcountryCode ?? string.Empty,
                Location = new Location() { Latitude = data.LatValue, Longitude = data.LonValue },
                FeatureName = data.Address?.Road ?? string.Empty,
                Locality = data.Address?.Village ?? string.Empty,
                PostalCode = data.Address?.Postcode ?? string.Empty
            }
        };
    }

    public async Task<IEnumerable<Location>> GetLocationsAsync(string address)
    {
        var httpClient = BlazorEssentials.Provider.GetService<HttpClient>();
        if (httpClient != null)
            Client.BaseAddress = httpClient.BaseAddress;

        string url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(address)}";

        // Make the request to the Nominatim API
        var response = await Client.GetStringAsync(url);

        // Parse the JSON response
        var data = JsonSerializer.Deserialize(response, AvaeJsonSerializerContext.Default.IEnumerableNominatimResponse);
        if (data is null)
        {
            return [];
        }
        return new List<Location>(data.Select(d =>
            {
                return new Location()
                {
                    Latitude =  d.LatValue,
                    Longitude = d.LonValue
                };

            }));
    }
}