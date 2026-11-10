

using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// <see cref="IGeocoding"/> backed by the public OpenStreetMap Nominatim service.
/// Shared by the Avalonia and Blazor hosts.
/// </summary>
/// <remarks>
/// The public Nominatim instance is limited to about one request per second and requires an
/// identifying User-Agent: see https://operations.osmfoundation.org/policies/nominatim/.
/// Browsers ignore the User-Agent header set here and send their own: in a browser app, point this
/// class at your own instance or at a server-side proxy rather than at the public one.
/// Requests to the public instance are spaced by <see cref="MinimumInterval"/> (process-wide), time out
/// after <see cref="Timeout"/>, and 429/503 responses are retried after their Retry-After delay.
/// </remarks>
public class NominatimGeocoding : IGeocoding
{
    /// <summary>Base address of the public Nominatim instance.</summary>
    public const string DefaultBaseAddress = "https://nominatim.openstreetmap.org/";

    /// <summary>Time allowed for one request, retries included (10 seconds by default).</summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Minimum delay between two requests to the public instance (its policy: one per second).</summary>
    public static TimeSpan MinimumInterval { get; set; } = TimeSpan.FromSeconds(1);

    const int MaxAttempts = 3;

    static readonly Lazy<HttpClient> SharedClient = new(() =>
    {
        // The timeout is applied per call (see GetStringAsync) so that it also covers the retries.
        var client = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Essentieables/1.0 (+https://github.com/cedric56/Essentieables)");
        return client;
    });

    static readonly SemaphoreSlim PublicInstanceGate = new(1, 1);
    static DateTimeOffset s_nextPublicRequest = DateTimeOffset.MinValue;

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
        var json = await GetStringAsync(BuildReverseUrl(latitude, longitude, _baseAddress)).ConfigureAwait(false);
        return ParsePlacemarks(json);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Location>> GetLocationsAsync(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var json = await GetStringAsync(BuildSearchUrl(address, _baseAddress)).ConfigureAwait(false);
        return ParseLocations(json);
    }

    bool IsPublicInstance => string.Equals(_baseAddress, DefaultBaseAddress, StringComparison.OrdinalIgnoreCase);

    async Task<string> GetStringAsync(string url)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var token = timeout.Token;

        for (var attempt = 1; ; attempt++)
        {
            if (IsPublicInstance)
                await WaitForPublicInstanceSlotAsync(token).ConfigureAwait(false);

            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
            if ((response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable) && attempt < MaxAttempts)
            {
                var delay = GetRetryDelay(response, attempt);
                if (IsPublicInstance)
                    PushBackPublicInstance(delay);
                await Task.Delay(delay, token).ConfigureAwait(false);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }
    }

    internal static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null)
            ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        return delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;
    }

    static async Task WaitForPublicInstanceSlotAsync(CancellationToken token)
    {
        await PublicInstanceGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var wait = s_nextPublicRequest - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, token).ConfigureAwait(false);
            s_nextPublicRequest = DateTimeOffset.UtcNow + MinimumInterval;
        }
        finally
        {
            PublicInstanceGate.Release();
        }
    }

    static void PushBackPublicInstance(TimeSpan delay)
    {
        var next = DateTimeOffset.UtcNow + delay;
        if (next > s_nextPublicRequest)
            s_nextPublicRequest = next;
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
