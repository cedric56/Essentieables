

using Microsoft.JSInterop;
using Microsoft.Maui.Devices;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Device info derived from navigator.userAgentData (falling back to the user agent string), the screen and the GPU:
/// tablets (iPad and iPad Pro included) are told apart from phones, and the device model and vendor are reported when the
/// browser exposes them. Fetched once via <see cref="InitializeAsync"/> and cached, so the synchronous
/// <see cref="IDeviceInfo"/> surface never calls JS interop directly.
/// </summary>
internal sealed class BlazorDeviceInfo : IDeviceInfo
{
    /// <summary>The DevicePlatform reported by this backend.</summary>
    public static DevicePlatform BrowserPlatform { get; } = DevicePlatform.Create("Browser");

    /// <summary>The platform of the device the browser runs on (Linux and ChromeOS are created by name: MAUI has no constant for them).</summary>
    public static DevicePlatform LinuxPlatform { get; } = DevicePlatform.Create("Linux");
    public static DevicePlatform ChromeOSPlatform { get; } = DevicePlatform.Create("ChromeOS");

    public sealed record Brand(string brand, string Version);
    public sealed record DeviceInfoJson(
        string UserAgent, string Vendor, string Language, string Platform, bool Mobile, Brand[] Brands,
        string? OsName = null, string? OsVersion = null, string? DeviceVendor = null, string? DeviceModel = null,
        bool IsTablet = false, bool IsMobile = false, bool IsIPad = false, bool IsIPadPro = false, bool IsIPhone = false,
        bool IsAndroid = false, string? Architecture = null, string? GpuRenderer = null, string? GpuVendor = null);
    public sealed record Info(
        string BrowserName, string BrowserVersion, string OSPlatform, bool Mobile,
        string OperatingSystem = "", string OperatingSystemVersion = "", string DeviceVendor = "", string DeviceModel = "",
        bool Tablet = false, bool IPad = false, bool IPadPro = false, bool IPhone = false, bool Android = false,
        string Architecture = "", string GpuRenderer = "", string GpuVendor = "");

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

    internal static Info Parse(DeviceInfoJson raw)
    {
        // Prefer userAgentData brands (Chromium); otherwise sniff the UA string.
        string browserName = string.Empty, browserVersion = string.Empty;
        foreach (var brand in raw.Brands ?? [])
        {
            if (brand.brand.Contains("Not", StringComparison.OrdinalIgnoreCase) || brand.brand == "Chromium")
                continue;
            browserName = brand.brand;
            browserVersion = brand.Version;
            break;
        }
        if (browserName.Length == 0)
            (browserName, browserVersion) = SniffUserAgent(raw.UserAgent ?? string.Empty);

        return new Info(
            browserName, browserVersion, raw.Platform ?? string.Empty, raw.Mobile || raw.IsMobile,
            raw.OsName ?? string.Empty, raw.OsVersion ?? string.Empty, raw.DeviceVendor ?? string.Empty, raw.DeviceModel ?? string.Empty,
            raw.IsTablet || raw.IsIPad, raw.IsIPad, raw.IsIPadPro, raw.IsIPhone, raw.IsAndroid,
            raw.Architecture ?? string.Empty, raw.GpuRenderer ?? string.Empty, raw.GpuVendor ?? string.Empty);
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

    // ───────────────────────── Browser details ─────────────────────────

    /// <summary>Name of the browser (Chrome, Edge, Firefox, Safari…).</summary>
    public string BrowserName => _info.BrowserName;

    /// <summary>Version of the browser.</summary>
    public string BrowserVersion => _info.BrowserVersion;

    /// <summary>Operating system the browser runs on (Windows, macOS, iOS, Android, Linux, ChromeOS), empty when unknown.</summary>
    public string OperatingSystem => _info.OperatingSystem;

    /// <summary>Version of that operating system ("11" for Windows 11 on Chromium, "17.1.2" on iOS…), empty when unknown.</summary>
    public string OperatingSystemVersion => _info.OperatingSystemVersion;

    /// <summary>CPU architecture ("x86-64", "arm-64" on Chromium, "x64"/"arm64" from the user agent), empty when unknown.</summary>
    public string Architecture => _info.Architecture;

    /// <summary>GPU renderer reported by WebGL, empty when the browser hides it.</summary>
    public string GpuRenderer => _info.GpuRenderer;

    /// <summary>GPU vendor reported by WebGL, empty when the browser hides it.</summary>
    public string GpuVendor => _info.GpuVendor;

    public bool IsTablet => _info.Tablet;
    public bool IsIPad => _info.IPad;
    public bool IsIPadPro => _info.IPadPro;
    public bool IsIPhone => _info.IPhone;
    public bool IsAndroid => _info.Android;

    /// <summary>
    /// The platform of the device itself (Android, iOS, WinUI for Windows, macOS, Linux, ChromeOS), as opposed to
    /// <see cref="Platform"/>, which is always <see cref="BrowserPlatform"/>. <see cref="DevicePlatform.Unknown"/> when unknown.
    /// </summary>
    public DevicePlatform RealPlatform => _info switch
    {
        { Android: true } => DevicePlatform.Android,
        { IPad: true } or { IPhone: true } => DevicePlatform.iOS,
        { OperatingSystem: "iOS" } => DevicePlatform.iOS,
        { OperatingSystem: "Windows" } => DevicePlatform.WinUI,
        { OperatingSystem: "macOS" } => DevicePlatform.macOS,
        { OperatingSystem: "ChromeOS" } => ChromeOSPlatform,
        { OperatingSystem: "Linux" } => LinuxPlatform,
        _ => DevicePlatform.Unknown,
    };

    // ───────────────────────── IDeviceInfo (sync, cached) ─────────────────────────

    /// <summary>The device model when the browser exposes it (Android, iPhone/iPad from the screen and GPU), otherwise the browser name.</summary>
    public string Model => _info.DeviceModel.Length > 0 ? _info.DeviceModel : _info.BrowserName;

    public string Manufacturer =>
        _info.DeviceVendor.Length > 0 ? _info.DeviceVendor
        : _info.GpuVendor.Length > 0 && !_info.GpuVendor.StartsWith("WebKit", StringComparison.OrdinalIgnoreCase) ? _info.GpuVendor
        : _info.OSPlatform;

    public string Name => _info.BrowserName;
    public string VersionString => _info.BrowserVersion;

    public Version Version => Version.TryParse(VersionString, out var version)
        ? version
        : int.TryParse(VersionString.Split('.')[0], out var major) ? new Version(major, 0) : new Version(0, 0);

    public DevicePlatform Platform => BrowserPlatform;
    public DeviceIdiom Idiom => _info.Tablet ? DeviceIdiom.Tablet : _info.Mobile ? DeviceIdiom.Phone : DeviceIdiom.Desktop;
    public DeviceType DeviceType => DeviceType.Physical;
}
