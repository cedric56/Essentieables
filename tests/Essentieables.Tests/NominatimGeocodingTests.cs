// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using System.Linq;
using Xunit;

namespace Essentieables.Tests;

public class NominatimGeocodingTests
{
    // Trimmed real /reverse response: osm_id exceeds int.MaxValue and importance is a JSON number.
    const string ReverseJson = """
    {
      "place_id": 123456789,
      "licence": "Data © OpenStreetMap contributors, ODbL 1.0.",
      "osm_type": "node",
      "osm_id": 12345678901,
      "lat": "47.2181700",
      "lon": "-1.5541300",
      "class": "place",
      "type": "house",
      "place_rank": 30,
      "importance": 0.00009999999999995449,
      "addresstype": "place",
      "name": "",
      "display_name": "1, Place Royale, Nantes, Loire-Atlantique, Pays de la Loire, 44000, France",
      "address": {
        "house_number": "1",
        "road": "Place Royale",
        "city": "Nantes",
        "county": "Loire-Atlantique",
        "state": "Pays de la Loire",
        "ISO3166-2-lvl4": "FR-PDL",
        "postcode": "44000",
        "country": "France",
        "country_code": "fr"
      },
      "boundingbox": ["47.2181200", "47.2182200", "-1.5541800", "-1.5540800"]
    }
    """;

    const string SearchJson = """
    [
      { "place_id": 1, "osm_id": 9876543210, "lat": "48.8588897", "lon": "2.3200410", "importance": 0.88 },
      { "place_id": 2, "osm_id": 7444, "lat": "33.6617962", "lon": "-95.555513", "importance": 0.5 }
    ]
    """;

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    public void Reverse_url_uses_a_dot_whatever_the_culture(string culture)
    {
        using var _ = new CultureScope(culture);

        var url = NominatimGeocoding.BuildReverseUrl(47.21817, -1.55413);

        Assert.Equal("https://nominatim.openstreetmap.org/reverse?format=json&lat=47.21817&lon=-1.55413&addressdetails=1", url);
    }

    [Fact]
    public void Search_url_escapes_the_address()
    {
        var url = NominatimGeocoding.BuildSearchUrl("1 place Royale & co, Nantes");

        Assert.Equal("https://nominatim.openstreetmap.org/search?format=json&q=1%20place%20Royale%20%26%20co%2C%20Nantes", url);
    }

    [Fact]
    public void Reverse_response_is_mapped_to_a_placemark()
    {
        using var _ = new CultureScope("fr-FR");

        var placemark = Assert.Single(NominatimGeocoding.ParsePlacemarks(ReverseJson));

        Assert.Equal("France", placemark.CountryName);
        Assert.Equal("FR", placemark.CountryCode);
        Assert.Equal("Nantes", placemark.Locality);
        Assert.Equal("Place Royale", placemark.FeatureName);
        Assert.Equal("Pays de la Loire", placemark.AdminArea);
        Assert.Equal("Loire-Atlantique", placemark.SubAdminArea);
        Assert.Equal("44000", placemark.PostalCode);
        Assert.Equal(47.21817, placemark.Location.Latitude, 5);
        Assert.Equal(-1.55413, placemark.Location.Longitude, 5);
    }

    [Theory]
    [InlineData("""{ "town": "Clisson" }""", "Clisson")]
    [InlineData("""{ "village": "Gétigné" }""", "Gétigné")]
    [InlineData("""{ "hamlet": "La Barre" }""", "La Barre")]
    public void Locality_falls_back_from_city_to_smaller_places(string address, string expected)
    {
        var json = $$"""{ "lat": "47.1", "lon": "-1.2", "address": {{address}} }""";

        Assert.Equal(expected, Assert.Single(NominatimGeocoding.ParsePlacemarks(json)).Locality);
    }

    [Fact]
    public void Error_response_yields_no_placemark()
    {
        Assert.Empty(NominatimGeocoding.ParsePlacemarks("""{ "error": "Unable to geocode" }"""));
    }

    [Fact]
    public void Search_response_is_mapped_to_locations()
    {
        var locations = NominatimGeocoding.ParseLocations(SearchJson);

        Assert.Equal(2, locations.Count);
        Assert.Equal(48.8588897, locations[0].Latitude, 7);
        Assert.Equal(-95.555513, locations.Last().Longitude, 6);
    }
}
