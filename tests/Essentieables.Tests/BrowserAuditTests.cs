using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Essentieables.Tests;

/// <summary>The browser implementations: failures reported instead of swallowed (audit of 17/11/2026).</summary>
[Collection(StaticStateCollection.Name)]
public class BrowserAuditTests
{
    static BlazorEssentialsSession SessionWith(IJSObjectReference module) =>
        new(new ServiceCollection().BuildServiceProvider()) { Module = module };

    static async Task InSession(IJSObjectReference module, Func<Task> test)
    {
        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = SessionWith(module);
            await test();
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public async Task Sharing_refused_by_the_browser_is_reported()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<bool>("shareIsSupported", Arg.Any<object?[]?>()).Returns(new ValueTask<bool>(true));
        module.InvokeAsync<bool>("share", Arg.Any<object?[]?>())
              .Returns<ValueTask<bool>>(_ => throw new JSException("NotAllowedError: Must be handling a user gesture"));

        await InSession(module, async () =>
        {
            var share = new BlazorShare();
            await share.InitializeAsync();

            await Assert.ThrowsAsync<PermissionException>(() => share.RequestAsync(new ShareTextRequest("hello")));
        });
    }

    [Fact]
    public async Task Sharing_without_the_Web_Share_API_throws_FeatureNotSupported()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<bool>("shareIsSupported", Arg.Any<object?[]?>()).Returns(new ValueTask<bool>(false));

        await InSession(module, async () =>
        {
            var share = new BlazorShare();
            await share.InitializeAsync();

            await Assert.ThrowsAsync<FeatureNotSupportedException>(() => share.RequestAsync(new ShareTextRequest("hello")));
        });
    }

    [Fact]
    public async Task A_dismissed_share_sheet_is_not_an_error()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<bool>("shareIsSupported", Arg.Any<object?[]?>()).Returns(new ValueTask<bool>(true));
        // The module resolves with false on AbortError.
        module.InvokeAsync<bool>("share", Arg.Any<object?[]?>()).Returns(new ValueTask<bool>(false));

        await InSession(module, async () =>
        {
            var share = new BlazorShare();
            await share.InitializeAsync();
            await share.RequestAsync(new ShareTextRequest("hello"));
        });
    }

    [Fact]
    public async Task A_clipboard_refusal_is_reported_and_does_not_raise_the_change_event()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<IJSVoidResult>("clipboardWriteText", Arg.Any<object?[]?>())
              .Returns<ValueTask<IJSVoidResult>>(_ => throw new JSException("NotAllowedError: Write permission denied."));

        await InSession(module, async () =>
        {
            var clipboard = new BlazorClipboard();
            var changed = false;
            clipboard.ClipboardContentChanged += (_, _) => changed = true;

            await Assert.ThrowsAsync<PermissionException>(() => clipboard.SetTextAsync("x"));
            Assert.False(changed);
        });
    }

    [Fact]
    public async Task An_empty_clipboard_reads_null()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<string?>("clipboardReadText", Arg.Any<object?[]?>()).Returns(new ValueTask<string?>(""));

        await InSession(module, async () => Assert.Null(await new BlazorClipboard().GetTextAsync()));
    }

    [Fact]
    public async Task Preferences_written_before_the_module_reach_localStorage_once_it_is_loaded()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<Dictionary<string, string>>("prefsGetAll", Arg.Any<object?[]?>())
              .Returns(new ValueTask<Dictionary<string, string>>(new Dictionary<string, string>()));

        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = null;
            var preferences = new BlazorPreferences();
            preferences.Set("early", "value");

            BlazorEssentials.CurrentSession = SessionWith(module);
            await preferences.InitializeAsync();
            await preferences.FlushAsync();

            await module.Received(1).InvokeAsync<IJSVoidResult>("prefsSet",
                Arg.Is<object?[]?>(a => a != null && "maui:prefs:_default:early".Equals(a[0]) && "value".Equals(a[1])));
            Assert.Equal("value", preferences.Get<string?>("early", null));
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    [Fact]
    public void VersionTracking_waits_for_the_preferences()
    {
        var preferences = new BlazorPreferences(_ => null); // not loaded
        var appInfo = Substitute.For<IAppInfo>();
        appInfo.VersionString.Returns("2.0");
        appInfo.BuildString.Returns("2");
        var tracking = new BlazorVersionTracking(preferences, appInfo);

        tracking.Track();
        Assert.Empty(tracking.VersionHistory); // deferred, not tracked on an empty history

        // Loaded later with a history: the deferred Track sees it.
        var loaded = new BlazorPreferences(_ => new Dictionary<string, string>
        {
            [BlazorPreferences.GetStorageKey("VersionTracking.Versions", "versiontracking")] = "1.0",
            [BlazorPreferences.GetStorageKey("VersionTracking.Builds", "versiontracking")] = "1",
        });
        var tracking2 = new BlazorVersionTracking(loaded, appInfo);
        tracking2.Track();
        Assert.False(tracking2.IsFirstLaunchEver);
        Assert.Equal("1.0", tracking2.PreviousVersion);
    }

    [Fact]
    public void Content_type_of_a_file_created_without_one_is_resolved()
    {
        // On the portable MAUI build, FileBase.ContentType throws for such a file.
        var file = new ReadOnlyFile(Path.Combine(Path.GetTempPath(), "photo.png"));

        Assert.Equal("image/png", file.GetContentTypeOrDefault());
    }

    [Theory]
    [InlineData(1080, 2400, "", DisplayOrientation.Portrait)]
    [InlineData(2400, 1080, "", DisplayOrientation.Landscape)]
    [InlineData(0, 0, "", DisplayOrientation.Unknown)]
    [InlineData(1080, 2400, "landscape-primary", DisplayOrientation.Landscape)]
    public void Display_orientation_without_screen_orientation_comes_from_the_size(double width, double height, string type, DisplayOrientation expected)
    {
        Assert.Equal(expected, BlazorDeviceDisplay.Compute(width, height, 2.625, type).Orientation);
    }

    [Theory]
    [InlineData("Error: unsupported", typeof(FeatureNotSupportedException))]
    [InlineData("NotAllowedError: denied", typeof(PermissionException))]
    [InlineData("SpeechSynthesisError: not-allowed", typeof(PermissionException))]
    public void JS_errors_map_to_the_MAUI_exceptions(string message, Type expected)
    {
        Assert.IsType(expected, BlazorEssentials.MapJSException(new JSException(message), "Test"));
    }

    [Fact]
    public void Other_JS_errors_propagate_as_they_are()
    {
        Assert.Null(BlazorEssentials.MapJSException(new JSException("DataError: bad data"), "Test"));
    }
}
