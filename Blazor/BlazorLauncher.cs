// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;
/// <summary>
/// Launcher backed by window.open for web URLs and location.assign for
/// protocol-handler schemes (mailto:, tel:, sms:). Files open as blob URLs in a new tab.
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

    internal static async Task<bool> OpenFileBlobAsync(string base64, string? contentType, string name)
    {
        try
        {
            return await BlazorEssentials.Module.InvokeAsync<bool>("openFileBlob", base64, contentType, name);
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


        static readonly string[] NavigationSchemes = ["mailto", "tel", "sms"];

    public Task<bool> CanOpenAsync(Uri uri) =>
        Task.FromResult(uri.Scheme is "http" or "https" || NavigationSchemes.Contains(uri.Scheme));

    public async Task<bool> OpenAsync(Uri uri)
    {
        return NavigationSchemes.Contains(uri.Scheme)
            ? await NavigateToAsync(uri.AbsoluteUri)
            : await OpenUrlAsync(uri.AbsoluteUri);
    }

    public async Task<bool> OpenAsync(OpenFileRequest request)
    {
        if (request.File is null)
            return false;
        var bytes = await File.ReadAllBytesAsync(request.File.FullPath).ConfigureAwait(false);
        return await OpenFileBlobAsync(
            Convert.ToBase64String(bytes),
            request.File.ContentType,
            Path.GetFileName(request.File.FullPath));
    }

    public async Task<bool> TryOpenAsync(Uri uri) =>
        await CanOpenAsync(uri).ConfigureAwait(false) && await OpenAsync(uri).ConfigureAwait(false);
}

