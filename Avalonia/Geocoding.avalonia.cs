using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
namespace Essentieables;

class AvaeGeocoding : IGeocoding
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
        // Nominatim reverse geocoding URL
        string url = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude}&lon={longitude}&addressdetails=1");

        // Make the request to the Nominatim API
        var response = await Client.GetStringAsync(url);

        // Parse the JSON response
        var data = JsonSerializer.Deserialize(response, AvaeJsonSerializerContext.Default.PlacemarkResponseInterop);

        if (data is null)
        {
            return [];
        }

        // Return the full address (can also return specific components)
        return new List<Placemark>
        {
            new Placemark()
            {
                CountryName = data.Address?.Country ?? string.Empty,
                CountryCode = data.Address?.CcountryCode ?? string.Empty,
                Location = new Location() { Latitude = data.LatValue, Longitude = data.LonValue },
                FeatureName = data.Address?.Road ?? string.Empty,
                Locality = data.Address?.City ?? data.Address?.Town ?? data.Address?.Village ?? data.Address?.Hamlet ?? string.Empty,
                PostalCode = data.Address?.Postcode ?? string.Empty
            }
        };
    }

    public async Task<IEnumerable<Location>> GetLocationsAsync(string address)
    {
        // Nominatim request URL
        string url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(address)}";

        // Make the request to the Nominatim API
        var response = await Client.GetStringAsync(url);

        // Parse the JSON response
        var data = JsonSerializer.Deserialize(response, AvaeJsonSerializerContext.Default.IEnumerableNominatimResponse);
        if (data is null)
        {
            return [];
        }
        return data.Select(d =>
            {
                return new Location()
                {
                    Latitude =  d.LatValue,
                    Longitude = d.LonValue
                };

            });
    }
}