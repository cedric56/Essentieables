

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Share backed by the Web Share API (navigator.share). Requires a secure context
/// and, in most browsers, a user gesture. File sharing uses Web Share Level 2.
/// </summary>
internal class BlazorShare : IAvaeShare
{
    bool _isSupported;
    public bool IsSupported => _isSupported;

    public async Task InitializeAsync()
    {
        try
        {
            _isSupported = await BlazorEssentials.Module.InvokeAsync<bool>("shareIsSupported");
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif
    }

    /// <summary>
    /// Shares through the browser. Dismissing the share sheet is not an error (as with MAUI); an unsupported browser throws
    /// <see cref="FeatureNotSupportedException"/>, a refusal (no user gesture, permission) <see cref="PermissionException"/>.
    /// </summary>
    public async Task RequestAsync(ShareTextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsSupported)
            throw new FeatureNotSupportedException("The Web Share API is not available in this browser.");

        try
        {
            await BlazorEssentials.Module.InvokeAsync<bool>("share", request.Title, request.Text, request.Uri).ConfigureAwait(false);
        }
        catch (JSException ex) when (BlazorEssentials.MapJSException(ex, "Sharing") is { } mapped)
        {
            throw mapped;
        }
    }

    public Task RequestAsync(ShareFileRequest request) =>
        ShareFilesAsync(request.Title, request.File is null ? [] : new List<ShareFile>() { request.File });

    public Task RequestAsync(ShareMultipleFilesRequest request) =>
        ShareFilesAsync(request.Title, request.Files ?? []);

    public Task RequestAsync(string title, IEnumerable<FileBase> files)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentNullException.ThrowIfNull(files, nameof(files));

        // Convert the enumerable to a list to avoid multiple enumeration and get accurate count
        var shareFiles = new List<ShareFile>(files.Count());

        foreach (var file in files)
        {
            // Check if the file is an Avalonia-specific file result
            if (file is BlazorFileResult b)
                shareFiles.Add(new BlazorShareFile(b));
            else
                // Not new ShareFile(file): its copy reads ContentType, which throws on the portable build without one.
                shareFiles.Add(new ShareFile(file.FullPath, file.GetContentTypeOrDefault()));
        }

        // Execute the native share request with the converted files
        return RequestAsync(new ShareMultipleFilesRequest()
        {
            Title = title,
            Files = shareFiles
        });
    }

    async Task ShareFilesAsync(string? title, IReadOnlyList<ShareFile> files)
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException("The Web Share API is not available in this browser.");
        if (files.Count == 0)
            throw new ArgumentException("No files were provided to share.");

        var names = new string[files.Count];
        var types = new string[files.Count];
        var contents = new string[files.Count];
        for (var i = 0; i < files.Count; i++)
        {
            names[i] = files[i].FileName;
            types[i] = files[i].GetContentTypeOrDefault();
            contents[i] = Convert.ToBase64String(
                files[i] is BlazorShareFile f ? 
                f.Data ?? await File.ReadAllBytesAsync(files[i].FullPath).ConfigureAwait(false) :                
                await File.ReadAllBytesAsync(files[i].FullPath).ConfigureAwait(false));
        }

        try
        {
            // False when the user dismissed the share sheet: not an error.
            await BlazorEssentials.Module.InvokeAsync<bool>("shareFiles",
                    title,
                    JsonSerializer.Serialize(names, AvaeJsonSerializerContext.Default.StringArray),
                    JsonSerializer.Serialize(types, AvaeJsonSerializerContext.Default.StringArray),
                    JsonSerializer.Serialize(contents, AvaeJsonSerializerContext.Default.StringArray)).ConfigureAwait(false);
        }
        catch (JSException ex) when (ex.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotSupportedException("This browser cannot share these files via the Web Share API.");
        }
        catch (JSException ex) when (BlazorEssentials.MapJSException(ex, "Sharing") is { } mapped)
        {
            throw mapped;
        }
    }
}

