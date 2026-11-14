using Microsoft.Maui.Devices;
using System;
using System.Collections.Generic;
using Xunit;

namespace Essentieables.Tests;

/// <summary>
/// The browser implementations shared by Blazor and Avalonia.Browser: what Avae.Browser.Essentials provided
/// (index.html meta tags, detailed device info, persistent directories, synchronous preferences) and Essentieables did not.
/// </summary>
public class BlazorDeviceInfoTests
{
    static BlazorDeviceInfo.DeviceInfoJson Json(
        string userAgent = "Mozilla/5.0", bool mobile = false, string? osName = null, string? osVersion = null,
        string? vendor = null, string? model = null, bool tablet = false, bool iPad = false, bool iPhone = false,
        bool android = false, string? gpuVendor = null)
        => new(userAgent, "", "en", "", mobile, [], osName, osVersion, vendor, model, tablet, mobile, iPad, false, iPhone, android,
               null, null, gpuVendor);

    [Fact]
    public void Tablets_are_not_phones()
    {
        var info = new BlazorDeviceInfo();
        Set(info, Json(osName: "Android", vendor: "Samsung", model: "SM-X700", tablet: true, android: true));

        Assert.Equal(DeviceIdiom.Tablet, info.Idiom);
        Assert.Equal("SM-X700", info.Model);
        Assert.Equal("Samsung", info.Manufacturer);
        Assert.Equal(DevicePlatform.Android, info.RealPlatform);
        Assert.Equal(BlazorDeviceInfo.BrowserPlatform, info.Platform);
    }

    [Fact]
    public void An_iPad_is_a_tablet_on_iOS()
    {
        var info = new BlazorDeviceInfo();
        Set(info, Json(osName: "iOS", osVersion: "17.1", vendor: "Apple", model: "iPad", iPad: true));

        Assert.Equal(DeviceIdiom.Tablet, info.Idiom);
        Assert.True(info.IsIPad);
        Assert.Equal(DevicePlatform.iOS, info.RealPlatform);
        Assert.Equal("17.1", info.OperatingSystemVersion);
    }

    [Fact]
    public void Without_a_device_model_the_browser_name_is_kept()
    {
        var info = new BlazorDeviceInfo();
        Set(info, Json(userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Gecko/20100101 Firefox/120.0", osName: "Windows", osVersion: "10"));

        Assert.Equal("Firefox", info.Model);
        Assert.Equal("Firefox", info.Name);
        Assert.Equal("120.0", info.VersionString);
        Assert.Equal(DeviceIdiom.Desktop, info.Idiom);
        Assert.Equal(DevicePlatform.WinUI, info.RealPlatform);
    }

    [Fact]
    public void Phones_are_phones()
    {
        var info = new BlazorDeviceInfo();
        Set(info, Json(mobile: true, osName: "iOS", vendor: "Apple", model: "iPhone", iPhone: true));

        Assert.Equal(DeviceIdiom.Phone, info.Idiom);
        Assert.Equal("Apple", info.Manufacturer);
    }

    static void Set(BlazorDeviceInfo info, BlazorDeviceInfo.DeviceInfoJson json)
        => typeof(BlazorDeviceInfo)
            .GetField("_info", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(info, BlazorDeviceInfo.Parse(json));
}

public class BlazorAppInfoTests
{
    [Fact]
    public void Meta_tags_of_index_html_win()
    {
        var info = new BlazorAppInfo();

        info.Apply(new BlazorAppInfo.AppInfoSnapshot("Page title", "example.org", false, false,
            MetaName: "My app", MetaPackage: "org.example.app", MetaVersion: "1.2.3", MetaBuild: "42"));

        Assert.Equal("My app", info.Name);
        Assert.Equal("org.example.app", info.PackageName);
        Assert.Equal("1.2.3", info.VersionString);
        Assert.Equal(new Version(1, 2, 3), info.Version);
        Assert.Equal("42", info.BuildString);
    }

    [Fact]
    public void Without_meta_tags_the_document_is_used()
    {
        var info = new BlazorAppInfo();

        info.Apply(new BlazorAppInfo.AppInfoSnapshot("Page title", "example.org", true, true));

        Assert.Equal("Page title", info.Name);
        Assert.Equal("example.org", info.PackageName);
        Assert.Equal(Microsoft.Maui.ApplicationModel.LayoutDirection.RightToLeft, info.RequestedLayoutDirection);
        Assert.Equal(Microsoft.Maui.ApplicationModel.AppTheme.Dark, info.RequestedTheme);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1+abc", "1.2.3")]
    [InlineData("7", "7.0")]
    public void Versions_are_parsed_leniently(string value, string expected)
    {
        Assert.Equal(Version.Parse(expected), BlazorAppInfo.ParseVersion(value));
    }

    [Fact]
    public void A_version_without_number_is_not_parsed()
    {
        Assert.Null(BlazorAppInfo.ParseVersion("dev"));
    }
}

public class BlazorBrowserStorageTests
{
    [Fact]
    public void Browser_directories_are_the_IndexedDB_stores_of_Avae_Browser_Essentials()
    {
        Assert.Equal("_cache", BlazorFileSystem.GetBrowserDirectoryName("cache"));
        Assert.Equal("_appdata", BlazorFileSystem.GetBrowserDirectoryName("appdata"));
    }

    [Fact]
    public void A_synchronous_loader_makes_stored_preferences_readable_before_the_JS_module()
    {
        string? requestedPrefix = null;
        var preferences = new BlazorPreferences(prefix =>
        {
            requestedPrefix = prefix;
            return new Dictionary<string, string>
            {
                [BlazorPreferences.GetStorageKey("theme", null)] = "dark",
                [BlazorPreferences.GetStorageKey("count", "shared")] = "3",
                // DateTime.ToBinary(): the format of the MAUI desktop implementations, imported in the browser.
                [BlazorPreferences.GetStorageKey("date", null)] = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc)
                    .ToBinary().ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
        });

        Assert.Equal("dark", preferences.Get<string?>("theme", null));
        Assert.True(preferences.IsLoaded);
        Assert.Equal(BlazorPreferences.KeyPrefix, requestedPrefix);
        Assert.Equal(3, preferences.Get("count", 0, "shared"));
        Assert.Equal(new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc), preferences.Get("date", DateTime.MinValue));
    }

    [Fact]
    public void Without_storage_the_synchronous_loader_leaves_the_load_to_InitializeAsync()
    {
        var preferences = new BlazorPreferences(_ => null);

        Assert.False(preferences.ContainsKey("theme"));
        Assert.False(preferences.IsLoaded);
    }
}

/// <summary>The fixes of the audit on the browser implementations.</summary>
public class BlazorAuditFixesTests
{
    [Fact]
    public void A_sensor_that_is_not_available_throws_to_the_caller_of_Start()
    {
        // Before InitializeAsync (or without the sensor), MAUI code catches FeatureNotSupportedException around Start:
        // it must be thrown synchronously, not lost in an async void.
        var accelerometer = new BlazorAccelerometer();

        Assert.Throws<Microsoft.Maui.ApplicationModel.FeatureNotSupportedException>(
            () => accelerometer.Start(Microsoft.Maui.Devices.Sensors.SensorSpeed.Default));
        Assert.False(accelerometer.IsMonitoring);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void An_empty_phone_number_throws_to_the_caller(string number)
    {
        Assert.ThrowsAny<ArgumentException>(() => new BlazorPhoneDialer().Open(number));
    }

    [Fact]
    public void Shared_names_with_a_colon_do_not_collide_with_keys_containing_one()
    {
        Assert.NotEqual(
            BlazorPreferences.GetStorageKey("b:c", "a"),
            BlazorPreferences.GetStorageKey("c", "a:b"));
        // Existing names are unchanged.
        Assert.Equal("maui:prefs:settings:theme", BlazorPreferences.GetStorageKey("theme", "settings"));
        Assert.Equal("maui:prefs:_default:theme", BlazorPreferences.GetStorageKey("theme", null));
    }
}
