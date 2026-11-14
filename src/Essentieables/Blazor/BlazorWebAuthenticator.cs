

using Microsoft.Maui.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using System.Linq;

namespace Essentieables;

/// <summary>
/// Two modes:
/// <list type="bullet">
/// <item><b>Start endpoint</b> (default in Blazor): the popup opens <c>/auth/start</c>, an endpoint of the server app that runs the
/// sign-in and redirects to <c>/auth/popup-complete</c> (see <see cref="BlazorEssentials.UseWebAuthenticator"/>).</item>
/// <item><b>Direct</b> (default in Avalonia.Browser, where there is usually no server): the popup opens
/// <see cref="WebAuthenticatorOptions.Url"/> and completes when the provider redirects it to
/// <see cref="WebAuthenticatorOptions.CallbackUrl"/>, which must be on the origin of the app.</item>
/// </list>
/// </summary>
internal class BlazorWebAuthenticator : IWebAuthenticator
{
    /// <summary>
    /// True: go through the <c>/auth/start</c> endpoint of the server. False: open the provider directly. Defaults to false in
    /// Avalonia.Browser apps, true in Blazor apps.
    /// </summary>
    public static bool UseStartEndpoint { get; set; } =
#if ESSENTIEABLES_AVALONIA_BROWSER
        false;
#else
        true;
#endif

    public Task<WebAuthenticatorResult> AuthenticateAsync(
        WebAuthenticatorOptions options)
        => AuthenticateAsync(options, CancellationToken.None);

    public async Task<WebAuthenticatorResult> AuthenticateAsync(
        WebAuthenticatorOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var authenticationUrl = options.Url;
        var callbackUrl = options.CallbackUrl;

        // The token only abandons the .NET side of the JS call: close the popup and stop its timers too.
        var module = BlazorEssentials.Module;
        using var cancellation = cancellationToken.Register(() =>
            BlazorEssentials.Forget(module.InvokeVoidAsync("authenticateCancel").AsTask(), "WebAuthenticator cancel"));

        string? callbackUri;
        if (UseStartEndpoint)
        {
            var url = BuildStartUrl(new Dictionary<string, string?>
            {
                ["returnUrl"] = "/auth/popup-complete",
                ["authenticationUrl"] = authenticationUrl?.ToString(),
                ["callbackUrl"] = callbackUrl?.ToString()
            });

            callbackUri = await BlazorEssentials.Module
                .InvokeAsync<string>(
                    "authenticate",
                    cancellationToken,
                    url);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(authenticationUrl);
            ArgumentNullException.ThrowIfNull(callbackUrl);

            callbackUri = await BlazorEssentials.Module
                .InvokeAsync<string>(
                    "authenticateDirect",
                    cancellationToken,
                    authenticationUrl.ToString(),
                    callbackUrl.ToString());
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(callbackUri))
        {
            throw new InvalidOperationException(
                "Authentication completed without a callback URI.");
        }

        return new WebAuthenticatorResult(new Uri(callbackUri), options.ResponseDecoder);
    }

    // Microsoft.AspNetCore.WebUtilities is not available in Blazor WebAssembly projects.
    internal static string BuildStartUrl(IEnumerable<KeyValuePair<string, string?>> query) =>
        "/auth/start?" + string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

    public async Task AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        var url = BuildStartUrl(new Dictionary<string, string?>
        {
            ["returnUrl"] = "/auth/popup-complete"
        });

        await BlazorEssentials.Module.InvokeAsync<string?>(
            "authenticate",
            cancellationToken,
            url);
    }
}

