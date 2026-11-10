using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Essentieables.Tests;

public class BlazorFilePickerTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "essentieables-tests", Guid.NewGuid().ToString("N"));

    public BlazorFilePickerTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\boot.ini", "boot.ini")]
    [InlineData("/abs/path/photo.jpg", "photo.jpg")]
    [InlineData("..", "file")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    [InlineData("report.pdf", "report.pdf")]
    public void Browser_names_stay_inside_the_pick_directory(string? name, string expected)
    {
        var path = BlazorFilePicker.GetSafePath(_directory, name);

        Assert.Equal(Path.GetFullPath(_directory), Path.GetDirectoryName(path));
        Assert.Equal(expected, Path.GetFileName(path));
    }

    [Fact]
    public void Duplicate_names_do_not_overwrite_each_other()
    {
        File.WriteAllText(Path.Combine(_directory, "a.txt"), "first");

        var path = BlazorFilePicker.GetSafePath(_directory, "a.txt");

        Assert.Equal("a (1).txt", Path.GetFileName(path));
    }

    [Theory]
    [InlineData(".png", ".png")]
    [InlineData("png", ".png")]
    [InlineData("*.JPG", ".jpg")]
    [InlineData("image/png", "image/png")]
    [InlineData("image/*", "image/*")]
    [InlineData("public.image", null)]
    [InlineData("com.adobe.pdf", null)]
    [InlineData(" ", null)]
    public void Filters_are_converted_to_accept_values(string value, string? expected)
    {
        Assert.Equal(expected, BlazorFilePicker.NormalizeFilter(value));
    }

    [Fact]
    public void File_types_of_other_platforms_are_used_without_a_browser_entry()
    {
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [".png", "*.jpg"],
            [DevicePlatform.Android] = ["image/png"],
            [DevicePlatform.iOS] = ["public.image"],
        });

        Assert.Equal(".png,.jpg,image/png", BlazorFilePicker.GetAccept(types));
    }

    [Fact]
    public void A_browser_entry_wins()
    {
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Create("Browser")] = ["application/pdf"],
            [DevicePlatform.WinUI] = [".png"],
        });

        Assert.Equal("application/pdf", BlazorFilePicker.GetAccept(types));
    }

    [Fact]
    public void No_file_types_accept_everything()
    {
        Assert.Null(BlazorFilePicker.GetAccept(null));
    }
}

public class BlazorLauncherTests
{
    [Theory]
    [InlineData("https://example.org/", true)]
    [InlineData("http://example.org/", true)]
    [InlineData("mailto:someone@example.org", true)]
    [InlineData("tel:+33123456789", true)]
    [InlineData("sms:+33123456789", true)]
    [InlineData("javascript:alert(document.cookie)", false)]
    [InlineData("data:text/html,hello", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("blob:https://example.org/1234", false)]
    [InlineData("vbscript:msgbox", false)]
    public async Task OpenAsync_applies_the_CanOpenAsync_policy(string url, bool allowed)
    {
        var launcher = new BlazorLauncher();
        var uri = new Uri(url);

        Assert.Equal(allowed, await launcher.CanOpenAsync(uri));
        if (!allowed)
        {
            // Refused before any JS call: no module is needed.
            Assert.False(await launcher.OpenAsync(uri));
        }
    }
}

public class NominatimRetryTests
{
    sealed class SequenceHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        int _index;
        public int Calls => _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responses[Math.Min(_index++, responses.Length - 1)]());
    }

    [Fact]
    public async Task Too_many_requests_is_retried_after_Retry_After()
    {
        var handler = new SequenceHandler(
            () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            },
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });

        var geocoding = new NominatimGeocoding(new HttpClient(handler), "https://geo.example.org/");

        var locations = await geocoding.GetLocationsAsync("Nantes");

        Assert.Empty(locations);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Persistent_unavailability_ends_with_an_error()
    {
        var handler = new SequenceHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        });

        var geocoding = new NominatimGeocoding(new HttpClient(handler), "https://geo.example.org/");

        await Assert.ThrowsAsync<HttpRequestException>(() => geocoding.GetLocationsAsync("Nantes"));
        Assert.Equal(3, handler.Calls);
    }
}
