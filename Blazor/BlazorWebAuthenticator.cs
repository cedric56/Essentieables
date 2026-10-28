using Microsoft.Maui.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.WebUtilities;

namespace Essentieables;

internal class BlazorWebAuthenticator : IWebAuthenticator
{
    public Task<WebAuthenticatorResult> AuthenticateAsync(
        WebAuthenticatorOptions options)
        => AuthenticateAsync(options, CancellationToken.None);

    public async Task<WebAuthenticatorResult> AuthenticateAsync(
        WebAuthenticatorOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        // Replace these with the actual properties on your options.
        var authenticationUrl = options.Url;
        var callbackUrl = options.CallbackUrl;

        var url = QueryHelpers.AddQueryString(
            "/auth/start",
            new Dictionary<string, string?>
            {
                ["returnUrl"] = "/auth/popup-complete",
                ["authenticationUrl"] = authenticationUrl?.ToString(),
                ["callbackUrl"] = callbackUrl?.ToString()
            });

        var callbackUri = await BlazorEssentials.Module
            .InvokeAsync<string>(
                "authenticate",
                cancellationToken,
                url);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(callbackUri))
        {
            throw new InvalidOperationException(
                "Authentication completed without a callback URI.");
        }

        return new WebAuthenticatorResult(new Uri(callbackUri));
    }

    public async Task AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(
            "/auth/start",
            "returnUrl",
            "/auth/popup-complete");

        await BlazorEssentials.Module.InvokeVoidAsync(
            "webAuthenticator.authenticate",
            cancellationToken,
            url);
    }
}

