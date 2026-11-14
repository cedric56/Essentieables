

using Xunit;

namespace Essentieables.Tests;

public class ResolverTests
{
    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("PHOTO.JPG", "image/jpeg")]
    [InlineData("/tmp/a.b/report.pdf", "application/pdf")]
    [InlineData("image.png", "image/png")]
    [InlineData("noextension", "application/octet-stream")]
    [InlineData("archive.unknownext", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    public void Content_type_is_resolved_from_the_extension(string fileName, string expected)
    {
        Assert.Equal(expected, ContentTypeResolver.Resolve(fileName));
    }

    [Theory]
    [InlineData("fr-FR", "France")]
    [InlineData("en-US", "United States")]
    [InlineData("xx-XX", null)]
    [InlineData("fr", "France")]
    [InlineData("de", "Germany")]
    [InlineData("FR-fr", "France")]
    [InlineData("en_US", "United States")]
    public void Country_is_resolved_from_the_locale(string locale, string? expected)
    {
        Assert.Equal(expected, CountryResolver.Resolve(locale));
    }

    [Fact]
    public void Without_a_locale_the_current_culture_is_used()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("France", CountryResolver.Resolve(null));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
