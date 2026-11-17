using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Microsoft.Maui.Devices.Sensors;
using NSubstitute;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Essentieables.Tests;

/// <summary>Fixes of the third audit (17/11/2026).</summary>
[Collection(StaticStateCollection.Name)]
public class AuditFixesTests
{
    static async Task InSession(IJSObjectReference module, Func<Task> test)
    {
        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = new BlazorEssentialsSession(new ServiceCollection().BuildServiceProvider()) { Module = module };
            await test();
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public void The_predefined_file_types_work_on_the_portable_build_and_target_the_browser()
    {
        Assert.Equal(".png,.jpg,.jpeg,.gif,.bmp,.webp,.heic,.heif,.tif,.tiff,.avif,image/*",
            BlazorFilePicker.GetAccept(EssentieablesFileTypes.Images));
        Assert.Equal(".pdf,application/pdf", BlazorFilePicker.GetAccept(EssentieablesPickOptions.Pdf.FileTypes));
        Assert.Same(EssentieablesFileTypes.Videos, EssentieablesPickOptions.Videos.FileTypes);
    }

    [Fact]
    public async Task A_secret_set_after_RemoveAll_is_not_deleted_by_it()
    {
        var removal = new TaskCompletionSource<IJSVoidResult>();
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<IJSVoidResult>("secureRemoveAll", Arg.Any<object?[]?>())
              .Returns(new ValueTask<IJSVoidResult>(removal.Task));

        await InSession(module, async () =>
        {
            var storage = new BlazorSecureStorage();
            storage.RemoveAll();
            var set = storage.SetAsync("token", "new");

            // The removal is still running: the new value waits for it.
            Assert.False(set.IsCompleted);
            await module.DidNotReceive().InvokeAsync<IJSVoidResult>("secureSet", Arg.Any<object?[]?>());

            removal.SetResult(null!);
            await set;

            Received.InOrder(() =>
            {
                module.InvokeAsync<IJSVoidResult>("secureRemoveAll", Arg.Any<object?[]?>());
                module.InvokeAsync<IJSVoidResult>("secureSet", Arg.Any<object?[]?>());
            });
        });
    }

    [Fact]
    public async Task A_denied_permission_stops_listening_and_a_lost_signal_does_not()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<int>("geoWatchStart", Arg.Any<object?[]?>()).Returns(new ValueTask<int>(7));

        await InSession(module, async () =>
        {
            var geolocation = new BlazorGeolocation();
            var failures = 0;
            geolocation.ListeningFailed += (_, _) => failures++;

            Assert.True(await geolocation.StartListeningForegroundAsync(new GeolocationListeningRequest()));

            geolocation.OnGeolocationError("position");
            Assert.True(geolocation.IsListeningForeground);

            geolocation.OnGeolocationError("permission");
            Assert.False(geolocation.IsListeningForeground);
            Assert.Equal(2, failures);
            await module.Received(1).InvokeAsync<IJSVoidResult>("geoWatchStop",
                Arg.Is<object?[]?>(a => a != null && 7.Equals(a[0])));
        });
    }

    [Theory]
    [InlineData("data/file.json", "data/file.json")]
    [InlineData("/data\\file.json", "data/file.json")]
    public void App_package_paths_stay_relative(string filename, string expected)
    {
        Assert.Equal(expected, BlazorFileSystem.NormalizePath(filename));
    }

    [Theory]
    [InlineData("https://other.example/secret.json")]
    [InlineData("data:text/plain,hello")]
    [InlineData("../secret.json")]
    [InlineData("data/../../secret.json")]
    [InlineData("%2E%2E/secret.json")]
    public void Paths_outside_the_app_package_are_refused(string filename)
    {
        Assert.Throws<ArgumentException>(() => BlazorFileSystem.NormalizePath(filename));
    }
}
