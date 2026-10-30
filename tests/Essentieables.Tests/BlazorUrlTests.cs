// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Xunit;

namespace Essentieables.Tests;

/// <summary>The Blazor sources are compiled into this test assembly, exactly as the package does for a Blazor host.</summary>
public class BlazorUrlTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void Map_url_uses_invariant_coordinates(string culture)
    {
        using var _ = new CultureScope(culture);

        var url = BlazorMap.BuildUrl(47.21817, -1.55413, new MapLaunchOptions());

        Assert.Equal("https://www.openstreetmap.org/?mlat=47.21817&mlon=-1.55413#map=15/47.21817/-1.55413", url);
    }

    [Fact]
    public void Map_url_switches_to_directions_when_navigating()
    {
        var url = BlazorMap.BuildUrl(47.2, -1.5, new MapLaunchOptions { NavigationMode = NavigationMode.Walking });

        Assert.StartsWith("https://www.openstreetmap.org/directions?engine=fossgis_osrm_foot", url);
    }

    [Fact]
    public void Auth_start_url_escapes_values_and_skips_missing_ones()
    {
        var url = BlazorWebAuthenticator.BuildStartUrl(new System.Collections.Generic.Dictionary<string, string?>
        {
            ["returnUrl"] = "/auth/popup-complete",
            ["authenticationUrl"] = "https://idp.example/authorize?a=1&b=2",
            ["callbackUrl"] = null,
        });

        Assert.Equal(
            "/auth/start?returnUrl=%2Fauth%2Fpopup-complete&authenticationUrl=https%3A%2F%2Fidp.example%2Fauthorize%3Fa%3D1%26b%3D2",
            url);
    }
}
