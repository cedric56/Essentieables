using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Microsoft.Maui.Storage;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Essentieables.Tests;

/// <summary>Fixes of the fourth audit (17/11/2026).</summary>
[Collection(StaticStateCollection.Name)]
public class AuditFourFixesTests
{
    static async Task InSession(IJSObjectReference? module, Func<Task> test)
    {
        var previous = BlazorEssentials.CurrentSession;
        try
        {
            BlazorEssentials.CurrentSession = module is null
                ? null
                : new BlazorEssentialsSession(new ServiceCollection().BuildServiceProvider()) { Module = module };
            await test();
        }
        finally
        {
            BlazorEssentials.CurrentSession = previous;
        }
    }

    // ── B1: OpenReadSafeAsync is reached through a FileResult variable ──────────────────────────────

    [Fact]
    public async Task A_file_typed_as_FileResult_is_read_on_the_portable_build()
    {
        var path = Path.Combine(Path.GetTempPath(), $"essentieables-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "hello");
        try
        {
            FileResult file = new FileResult(path);

            using (var stream = await file.OpenReadSafeAsync())
            using (var reader = new StreamReader(stream))
                Assert.Equal("hello", await reader.ReadToEndAsync());

            FileResult picked = new BlazorFileResult(path, "text/plain", "picked"u8.ToArray());
            using (var stream = await picked.OpenReadSafeAsync())
            using (var reader = new StreamReader(stream))
                Assert.Equal("picked", await reader.ReadToEndAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── B2: percent-encoded dot segments ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(".%2e/admin")]
    [InlineData("%2e./admin")]
    [InlineData("a/.%2E/secret")]
    [InlineData("a%2f..%2fsecret")]
    [InlineData("a%5c..%5csecret")]
    public void Encoded_dot_segments_and_separators_are_refused(string filename)
    {
        Assert.Throws<ArgumentException>(() => BlazorFileSystem.NormalizePath(filename));
    }

    [Fact]
    public void Encoded_names_that_stay_inside_the_package_are_kept()
    {
        Assert.Equal("data/my%20file.json", BlazorFileSystem.NormalizePath("data/my%20file.json"));
    }

    // ── B3: the loopback listener checks state, and listens on 127.0.0.1 without HttpListener ───────

    static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task Only_the_callback_with_the_state_of_the_sign_in_completes_it()
    {
        var port = FreePort();
        var callback = new Uri($"http://127.0.0.1:{port}/callback");
        var signIn = new Uri($"https://idp.example/authorize?client_id=app&state=s3cr%C3%A9t&redirect_uri={Uri.EscapeDataString(callback.ToString())}");
        var statuses = new List<HttpStatusCode>();

        var result = await AvaeWebAuthenticator.ListenAsync(signIn, callback, null, signInUrl =>
        {
            // The parameter is not named "_": "_ = Task.Run(…)" would then assign it instead of discarding.
            _ = Task.Run(async () =>
            {
                using var client = new HttpClient();
                // Another path (favicon), a forged callback, then the real one.
                foreach (var url in new[] { "/favicon.ico", "/callback?code=forged&state=other", "/callback?code=good&state=s3cr%C3%A9t" })
                {
                    using var response = await client.GetAsync($"http://127.0.0.1:{port}{url}");
                    lock (statuses)
                        statuses.Add(response.StatusCode);
                }
            });
            return Task.FromResult(true);
        }, CancellationToken.None);

        Assert.Equal("good", result.Properties["code"]);
        await WaitUntil(() => { lock (statuses) return statuses.Count == 3; });
        Assert.Equal(new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.OK }, statuses);
    }

    [Fact]
    public async Task Without_state_the_first_callback_completes_the_sign_in()
    {
        var port = FreePort();
        var callback = new Uri($"http://localhost:{port}/");

        var result = await AvaeWebAuthenticator.ListenAsync(new Uri("https://idp.example/authorize"), callback, null, signInUrl =>
        {
            // The parameter is not named "_": "_ = Task.Run(…)" would then assign it instead of discarding.
            _ = Task.Run(async () =>
            {
                using var client = new HttpClient();
                await client.GetAsync($"http://127.0.0.1:{port}/?code=abc");
            });
            return Task.FromResult(true);
        }, CancellationToken.None);

        Assert.Equal("abc", result.Properties["code"]);
    }

    [Fact]
    public async Task A_canceled_sign_in_stops_listening()
    {
        var port = FreePort();
        using var cancellation = new CancellationTokenSource();
        var task = AvaeWebAuthenticator.ListenAsync(new Uri("https://idp.example/authorize?state=x"),
            new Uri($"http://127.0.0.1:{port}/cb"), null, _ => Task.FromResult(true), cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        // The port is free again.
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        listener.Stop();
    }

    [Theory]
    [InlineData("GET /cb?code=1 HTTP/1.1", "http://127.0.0.1:5000/cb?code=1")]
    [InlineData("GET /cb/?code=1 HTTP/1.1", "http://127.0.0.1:5000/cb/?code=1")]
    [InlineData("POST /cb HTTP/1.1", null)]
    [InlineData("GET //evil.example/cb HTTP/1.1", null)]
    [InlineData("GET http://evil.example/cb HTTP/1.1", null)]
    [InlineData("GET /other HTTP/1.1", null)]
    public void Only_GET_requests_on_the_callback_path_are_callbacks(string requestLine, string? expected)
    {
        Assert.Equal(expected, AvaeWebAuthenticator.ParseCallback(requestLine, new Uri("http://127.0.0.1:5000/cb"))?.ToString());
    }

    static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(50);
    }

    // ── B8: the launcher reports a missing initialization instead of returning false ─────────────────

    [Fact]
    public async Task The_launcher_throws_when_not_initialized()
    {
        await InSession(null, async () =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => new BlazorLauncher().OpenAsync(new Uri("https://example.org/"))));
    }

    [Fact]
    public async Task A_launch_refused_by_the_browser_returns_false()
    {
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<bool>("openUrl", Arg.Any<object?[]?>())
              .Returns<ValueTask<bool>>(_ => throw new JSException("Error: blocked"));

        await InSession(module, async () => Assert.False(await new BlazorLauncher().OpenAsync(new Uri("https://example.org/"))));
    }

    // ── B13: preferences of the other tabs ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Preferences_follow_the_other_tabs_and_Clear_removes_their_keys_too()
    {
        var key = BlazorPreferences.GetStorageKey("theme", null);
        var module = Substitute.For<IJSObjectReference>();
        module.InvokeAsync<Dictionary<string, string>>("prefsGetAll", Arg.Any<object?[]?>())
              .Returns(new ValueTask<Dictionary<string, string>>(new Dictionary<string, string> { [key] = "light" }));

        await InSession(module, async () =>
        {
            var preferences = new BlazorPreferences();
            await preferences.InitializeAsync();
            await module.Received(1).InvokeAsync<IJSVoidResult>("prefsWatch", Arg.Any<object?[]?>());

            preferences.OnStorageChanged(key, "dark");
            Assert.Equal("dark", preferences.Get("theme", ""));
            preferences.OnStorageChanged(key, null);
            Assert.False(preferences.ContainsKey("theme"));

            preferences.Clear();
            await preferences.FlushAsync();
            await module.Received(1).InvokeAsync<IJSVoidResult>("prefsClear",
                Arg.Is<object?[]?>(a => a != null && "maui:prefs:_default:".Equals(a[0])));
        });
    }
}
