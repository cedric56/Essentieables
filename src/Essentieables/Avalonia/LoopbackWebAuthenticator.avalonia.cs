// Desktop only: sockets are not available in the browser, where the shared browser implementation is used.
#if !ESSENTIEABLES_AVALONIA_BROWSER

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Authentication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Desktop web authenticator. When the callback is an <c>http://localhost:port/…</c> or <c>http://127.0.0.1:port/…</c> URL
/// (the "loopback" redirect most identity providers recommend for desktop apps), the sign-in page opens in the system browser
/// and the callback is received on the loopback interface, as Avae.Windows.Essentials and Avae.macOS.Essentials did:
/// providers such as Google refuse embedded web views. Any other callback (custom scheme, https) is handled by
/// <paramref name="inner"/>.
/// </summary>
/// <remarks>
/// A plain socket listener is used rather than <see cref="HttpListener"/>: on Windows, HttpListener needs an URL reservation
/// (administrator rights) for <c>http://127.0.0.1:port/</c>, and rejects a <c>Host: 127.0.0.1</c> request on a
/// <c>localhost</c> prefix. When the sign-in URL has a <c>state</c> parameter, only a callback carrying the same
/// <c>state</c> completes the sign-in: a request forged by another local process or a web page is answered with 400 and
/// ignored. Use <c>state</c> and PKCE in the sign-in URL.
/// </remarks>
internal sealed class AvaeWebAuthenticator(IWebAuthenticator inner) : IWebAuthenticator
{
    /// <summary>How long the loopback flow waits for the callback (5 minutes by default).</summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The page shown in the browser once the callback is received.</summary>
    public static string CompletionPage { get; set; } =
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>Signed in</title>" +
        "<style>body{font-family:sans-serif;margin:3em}h1{color:green;font-size:20px}</style></head>" +
        "<body><h1>You can now close this window.</h1><script>window.close()</script></body></html>";

    const int MaxRequestHeaderBytes = 16 * 1024;
    static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(30);

    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions)
        => AuthenticateAsync(webAuthenticatorOptions, CancellationToken.None);

    public Task<WebAuthenticatorResult> AuthenticateAsync(WebAuthenticatorOptions webAuthenticatorOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webAuthenticatorOptions);

        return webAuthenticatorOptions.Url is { } url && webAuthenticatorOptions.CallbackUrl is { } callback && IsLoopback(callback)
            ? ListenAsync(url, callback, webAuthenticatorOptions.ResponseDecoder, Launcher.Default.OpenAsync, cancellationToken)
            : inner.AuthenticateAsync(webAuthenticatorOptions, cancellationToken);
    }

    internal static bool IsLoopback(Uri callback)
        => callback.IsAbsoluteUri
           && callback.Scheme == Uri.UriSchemeHttp
           && (callback.IsLoopback || string.Equals(callback.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    /// <summary>Opens <paramref name="url"/> with <paramref name="open"/> and waits for the callback.</summary>
    internal static async Task<WebAuthenticatorResult> ListenAsync(
        Uri url, Uri callback, IWebAuthenticatorResponseDecoder? decoder, Func<Uri, Task<bool>> open, CancellationToken cancellationToken)
    {
        var expectedState = GetQueryValue(url, "state");
        var listeners = StartListeners(callback);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var received = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            foreach (var listener in listeners)
                _ = AcceptAsync(listener, callback, expectedState, received, timeout.Token);

            timeout.CancelAfter(Timeout);
            if (!await open(url).ConfigureAwait(false))
                throw new InvalidOperationException($"Unable to open '{url}' in the browser.");

            using (timeout.Token.Register(() => received.TrySetCanceled(timeout.Token)))
            {
                try
                {
                    return new WebAuthenticatorResult(await received.Task.ConfigureAwait(false), decoder);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"No authentication callback received on {callback} within {Timeout}.", ex);
                }
            }
        }
        finally
        {
            timeout.Cancel();
            foreach (var listener in listeners)
                listener.Stop();
        }
    }

    /// <summary>
    /// The loopback addresses the callback host resolves to: both 127.0.0.1 and ::1 for <c>localhost</c> (the browser may
    /// use either), the address itself otherwise.
    /// </summary>
    static List<TcpListener> StartListeners(Uri callback)
    {
        IPAddress[] addresses = string.Equals(callback.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            ? [IPAddress.Loopback, IPAddress.IPv6Loopback]
            : [IPAddress.Parse(callback.Host.Trim('[', ']'))];

        var listeners = new List<TcpListener>();
        SocketException? failure = null;
        foreach (var address in addresses)
        {
            var listener = new TcpListener(address, callback.Port);
            try
            {
                listener.Start();
                listeners.Add(listener);
            }
            catch (SocketException ex)
            {
                // IPv6 disabled, or the port is in use: the other address may still work.
                failure ??= ex;
            }
        }

        if (listeners.Count == 0)
            throw new InvalidOperationException($"Unable to listen for the authentication callback on {callback}.", failure);
        return listeners;
    }

    static async Task AcceptAsync(TcpListener listener, Uri callback, string? expectedState, TaskCompletionSource<Uri> received, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            // One task per connection: browsers open idle (speculative) connections that never send a request.
            _ = HandleAsync(client, callback, expectedState, received, token);
        }
    }

    static async Task HandleAsync(TcpClient client, Uri callback, string? expectedState, TaskCompletionSource<Uri> received, CancellationToken token)
    {
        using (client)
        {
            try
            {
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                requestTimeout.CancelAfter(RequestReadTimeout);
                var stream = client.GetStream();

                var request = await ReadRequestLineAsync(stream, requestTimeout.Token).ConfigureAwait(false);
                var requestUri = request is { } line ? ParseCallback(line, callback) : null;
                if (requestUri is null)
                {
                    await RespondAsync(stream, 404, "Not Found", "", requestTimeout.Token).ConfigureAwait(false);
                    return;
                }

                if (expectedState is not null && !string.Equals(GetQueryValue(requestUri, "state"), expectedState, StringComparison.Ordinal))
                {
                    // Not the answer to this sign-in (login CSRF): refused, and the flow keeps waiting.
                    await RespondAsync(stream, 400, "Bad Request", "", requestTimeout.Token).ConfigureAwait(false);
                    return;
                }

                await RespondAsync(stream, 200, "OK", CompletionPage, requestTimeout.Token).ConfigureAwait(false);
                received.TrySetResult(requestUri);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // Connection closed or idle: nothing to answer.
            }
        }
    }

    /// <summary>The request line ("GET /path?query HTTP/1.1"), once the whole header is received; null for a malformed request.</summary>
    static async Task<string?> ReadRequestLineAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[MaxRequestHeaderBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), token).ConfigureAwait(false);
            if (read == 0)
                return null;
            length += read;

            var text = Encoding.ASCII.GetString(buffer, 0, length);
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
                return text[..text.IndexOf("\r\n", StringComparison.Ordinal)];
        }
        return null;
    }

    /// <summary>The callback URL of a GET request on the callback path; null for any other request (favicon…).</summary>
    internal static Uri? ParseCallback(string requestLine, Uri callback)
    {
        var parts = requestLine.Split(' ');
        if (parts.Length != 3 || parts[0] != "GET" || !parts[1].StartsWith('/') || parts[1].StartsWith("//", StringComparison.Ordinal))
            return null;

        if (!Uri.TryCreate(callback.GetLeftPart(UriPartial.Authority) + parts[1], UriKind.Absolute, out var requestUri))
            return null;

        return string.Equals(requestUri.AbsolutePath.TrimEnd('/'), callback.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal)
            ? requestUri
            : null;
    }

    internal static string? GetQueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString((separator < 0 ? pair : pair[..separator]).Replace('+', ' '));
            if (key == name)
                return separator < 0 ? "" : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
        }
        return null;
    }

    static async Task RespondAsync(NetworkStream stream, int status, string reason, string html, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(body, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
}
#endif
