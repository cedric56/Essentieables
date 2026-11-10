

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Essentieables;
/// <summary>
/// Launcher backed by window.open for web URLs and location.assign for
/// protocol-handler schemes (mailto:, tel:, sms:); every other scheme is refused. Files are
/// streamed to the browser and open as blob URLs in a new tab (active content is downloaded).
/// </summary>
internal class BlazorLauncher : ILauncher
{
    internal static async Task<bool> OpenUrlAsync(string url)
    {
        try
        {
            return await BlazorEssentials.Module.InvokeAsync<bool>("openUrl", url);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
#else
        catch
        {
            return false;
        }
#endif
    }

    internal static async Task<bool> NavigateToAsync(string url)
    {
        try
        {
            return await BlazorEssentials.Module.InvokeAsync<bool>("navigateTo", url);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
#else
        catch
        {
            return false;
        }
#endif
    }

    static readonly string[] WebSchemes = ["http", "https"];
    static readonly string[] NavigationSchemes = ["mailto", "tel", "sms"];

    /// <summary>The only schemes this launcher opens; javascript:, data:, blob:, file:… are refused.</summary>
    internal static bool IsAllowed(Uri uri) =>
        uri.IsAbsoluteUri && (WebSchemes.Contains(uri.Scheme) || NavigationSchemes.Contains(uri.Scheme));

    public Task<bool> CanOpenAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return Task.FromResult(IsAllowed(uri));
    }

    public async Task<bool> OpenAsync(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // Same policy as CanOpenAsync: calling OpenAsync directly must not bypass it
        // (the JS module checks the scheme again).
        if (!IsAllowed(uri))
            return false;

        return NavigationSchemes.Contains(uri.Scheme)
            ? await NavigateToAsync(uri.AbsoluteUri)
            : await OpenUrlAsync(uri.AbsoluteUri);
    }

    public async Task<bool> OpenAsync(OpenFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.File is null)
            return false;

        // Picked and captured files are on disk (BlazorFileResult), like any other path given here.
        await using var stream = File.OpenRead(request.File.FullPath);
        using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);

        // HTML, SVG, XML and script files are downloaded instead of opened: a blob: URL has the
        // origin of the app, so their scripts would run with access to it.
        return await BlazorEssentials.Module.InvokeAsync<bool>(
            "openFileStream",
            streamReference,
            request.File.ContentType,
            request.File.FileName).ConfigureAwait(false);
    }

    public async Task<bool> TryOpenAsync(Uri uri) =>
        await CanOpenAsync(uri).ConfigureAwait(false) && await OpenAsync(uri).ConfigureAwait(false);
}
