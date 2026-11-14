// Desktop only: HttpListener is not available in the browser, where the shared browser implementation is used.
#if !ESSENTIEABLES_AVALONIA_BROWSER

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Desktop web authenticator. When the callback is an <c>http://localhost:port/…</c> URL (the "loopback" redirect most
/// identity providers recommend for desktop apps), the sign-in page opens in the system browser and the callback is
/// received by an <see cref="HttpListener"/>, as Avae.Windows.Essentials and Avae.macOS.Essentials did: providers such as
/// Google refuse embedded web views. Any other callback (custom scheme, https) is handled by <paramref name="inner"/>.
/// </summary>
internal sealed class AvaeWebAuthenticator(IWebAuthenticator inner) : IWebAuthenticator
{
    /// <summary>How long the loopback flow waits for the callback (5 minutes by default).</summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The page shown in the browser once the callback is received.</summary>
    public static string CompletionPage { get; set; } =
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>Signed in</title>" +
        "<style>body{font-family:sans-serif;margin:3em}h1{color:green;font-size:20px}</style></head>" +
        "<body><h1>You can now close this window.</h1><script>window.close()</script></body></html>";

    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions)
        => AuthenticateAsync(webAuthenticatorOptions, CancellationToken.None);

    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webAuthenticatorOptions);

        return webAuthenticatorOptions.Url is { } url && webAuthenticatorOptions.CallbackUrl is { } callback && IsLoopback(callback)
            ? ListenAsync(url, callback, webAuthenticatorOptions.ResponseDecoder, cancellationToken)
            : inner.AuthenticateAsync(webAuthenticatorOptions, cancellationToken);
    }

    internal static bool IsLoopback(Uri callback)
        => callback.IsAbsoluteUri
           && callback.Scheme == Uri.UriSchemeHttp
           && (callback.IsLoopback || string.Equals(callback.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    static async Task<WebAuthenticatorResult> ListenAsync(Uri url, Uri callback, IWebAuthenticatorResponseDecoder? decoder, CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        // The whole port: favicon and other requests of the browser are answered with 404 below.
        listener.Prefixes.Add($"{callback.Scheme}://{callback.Host}:{callback.Port}/");
        listener.Start();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        // Stops GetContextAsync, which takes no token.
        using var registration = timeout.Token.Register(listener.Stop);

        if (!await Launcher.Default.OpenAsync(url).ConfigureAwait(false))
            throw new InvalidOperationException($"Unable to open '{url}' in the browser.");

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                var requestUrl = context.Request.Url;

                if (requestUrl is null || !string.Equals(requestUrl.AbsolutePath.TrimEnd('/'), callback.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var buffer = Encoding.UTF8.GetBytes(CompletionPage);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                context.Response.Close();

                return new WebAuthenticatorResult(requestUrl, decoder);
            }
        }
        catch (Exception ex) when (timeout.IsCancellationRequested && ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"No authentication callback received on {callback} within {Timeout}.", ex);
        }
    }
}
#endif
