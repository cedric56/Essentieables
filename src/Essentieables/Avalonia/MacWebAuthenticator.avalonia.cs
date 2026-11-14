// macOS only: compiled for net10.0-macos (MACOS is defined by the SDK).
#if MACOS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using AppKit;
using AuthenticationServices;
using Foundation;
using Microsoft.Maui.Authentication;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// ASWebAuthenticationSession, as in Avae.macOS.Essentials: the system sign-in sheet, which shares the cookies of Safari unless
/// <see cref="WebAuthenticatorOptions.PrefersEphemeralWebBrowserSession"/> is set, and which providers such as Google accept.
/// The session receives the custom-scheme callback itself (no Info.plist entry needed). <c>http://localhost</c> callbacks are
/// handled before, by <see cref="AvaeWebAuthenticator"/>.
/// </summary>
internal sealed class AvaeMacWebAuthenticator : IWebAuthenticator
{
    const int CanceledLoginErrorCode = 1; // ASWebAuthenticationSessionErrorCodeCanceledLogin

    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions)
        => AuthenticateAsync(webAuthenticatorOptions, CancellationToken.None);

    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webAuthenticatorOptions);
        var url = webAuthenticatorOptions.Url ?? throw new ArgumentException("Url is required.", nameof(webAuthenticatorOptions));
        var callbackUrl = webAuthenticatorOptions.CallbackUrl ?? throw new ArgumentException("CallbackUrl is required.", nameof(webAuthenticatorOptions));

        return AvaeMainThread.InvokeOnMainThreadAsync(() => StartAsync(url, callbackUrl, webAuthenticatorOptions, cancellationToken));
    }

    static async Task<WebAuthenticatorResult> StartAsync(Uri url, Uri callbackUrl, WebAuthenticatorOptions options, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<WebAuthenticatorResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Completed(NSUrl? callback, NSError? error)
        {
            if (error is null && callback?.AbsoluteString is { } absolute)
                tcs.TrySetResult(new WebAuthenticatorResult(new Uri(absolute), options.ResponseDecoder));
            else if (error is not null && error.Domain == ASWebAuthenticationSessionErrorCodeDomain && error.Code == CanceledLoginErrorCode)
                tcs.TrySetCanceled();
            else if (error is not null)
                tcs.TrySetException(new NSErrorException(error));
            else
                tcs.TrySetException(new InvalidOperationException("Authentication completed without a callback URL."));
        }

        using var session = new ASWebAuthenticationSession(new NSUrl(url.AbsoluteUri), callbackUrl.Scheme, Completed)
        {
            PrefersEphemeralWebBrowserSession = options.PrefersEphemeralWebBrowserSession,
        };
        using var context = new PresentationContext(MacCamera.GetParentWindow());
        session.PresentationContextProvider = context;

        using var registration = cancellationToken.Register(() =>
        {
            AvaeMainThread.BeginInvokeOnMainThread(session.Cancel);
            tcs.TrySetCanceled(cancellationToken);
        });

        if (!session.Start())
            throw new InvalidOperationException("The authentication session could not be started.");

        return await tcs.Task.ConfigureAwait(true);
    }

    // ASWebAuthenticationSessionErrorDomain
    static readonly string ASWebAuthenticationSessionErrorCodeDomain = "com.apple.AuthenticationServices.WebAuthenticationSession";

    sealed class PresentationContext(NSWindow? window) : NSObject, IASWebAuthenticationPresentationContextProviding
    {
        [Export("presentationAnchorForWebAuthenticationSession:")]
        public NSWindow GetPresentationAnchor(ASWebAuthenticationSession session)
            => window ?? NSApplication.SharedApplication.KeyWindow ?? NSApplication.SharedApplication.MainWindow!;
    }
}
#endif
