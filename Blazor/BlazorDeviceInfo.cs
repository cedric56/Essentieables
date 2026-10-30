using Microsoft.JSInterop;
using Microsoft.Maui.Devices;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Device info derived from navigator.userAgentData (falling back to the user agent string).
/// Fetched once via <see cref="InitializeAsync"/> and cached, so the synchronous
/// <see cref="IDeviceInfo"/> surface never calls JS interop directly.
/// </summary>
internal sealed class BlazorDeviceInfo : IDeviceInfo
{
    /// <summary>The DevicePlatform reported by this backend.</summary>
    public static DevicePlatform BrowserPlatform { get; } = DevicePlatform.Create("Browser");

    public sealed record Brand(string brand, string Version);
    public sealed record DeviceInfoJson(
        string UserAgent, string Vendor, string Language, string Platform, bool Mobile, Brand[] Brands);
    public sealed record Info(string BrowserName, string BrowserVersion, string OSPlatform, bool Mobile);

    private static readonly Info Default = new("Browser", string.Empty, string.Empty, false);

    Info _info = Default;
    /// <summary>Fetches and caches the device info. Idempotent. Must complete before any property is read.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var raw = await BlazorEssentials.Module.InvokeAsync<DeviceInfoJson>("getDeviceInfo");
            if (raw is not null)
                _info = Parse(raw);
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

    private static Info Parse(DeviceInfoJson raw)
    {
        // Prefer userAgentData brands (Chromium); otherwise sniff the UA string.
        string browserName = string.Empty, browserVersion = string.Empty;
        foreach (var brand in raw.Brands)
        {
            if (brand.brand.Contains("Not", StringComparison.OrdinalIgnoreCase) || brand.brand == "Chromium")
                continue;
            browserName = brand.brand;
            browserVersion = brand.Version;
            break;
        }
        if (browserName.Length == 0)
            (browserName, browserVersion) = SniffUserAgent(raw.UserAgent);

        return new Info(browserName, browserVersion, raw.Platform, raw.Mobile);
    }

    private static (string Name, string Version) SniffUserAgent(string userAgent)
    {
        foreach (var candidate in new[] { "Firefox/", "Edg/", "Chrome/", "Version/" })
        {
            var index = userAgent.IndexOf(candidate, StringComparison.Ordinal);
            if (index < 0)
                continue;
            var version = userAgent[(index + candidate.Length)..].Split(' ', ')')[0];
            var name = candidate switch
            {
                "Edg/" => "Edge",
                "Version/" => "Safari",
                _ => candidate.TrimEnd('/'),
            };
            return (name, version);
        }
        return ("Browser", string.Empty);
    }

    // ───────────────────────── IDeviceInfo (sync, cached) ─────────────────────────

    public string Model => _info.BrowserName;
    public string Manufacturer => _info.OSPlatform;
    public string Name => _info.BrowserName;
    public string VersionString => _info.BrowserVersion;

    public Version Version => Version.TryParse(VersionString, out var version)
        ? version
        : int.TryParse(VersionString.Split('.')[0], out var major) ? new Version(major, 0) : new Version(0, 0);

    public DevicePlatform Platform => BrowserPlatform;
    public DeviceIdiom Idiom => _info.Mobile ? DeviceIdiom.Phone : DeviceIdiom.Desktop;
    public DeviceType DeviceType => DeviceType.Physical;
}