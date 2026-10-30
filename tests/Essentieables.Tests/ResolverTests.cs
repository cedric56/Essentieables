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
    public void Country_is_resolved_from_the_locale(string locale, string? expected)
    {
        Assert.Equal(expected, CountryResolver.Resolve(locale));
    }
}
