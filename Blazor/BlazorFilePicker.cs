// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.JSInterop;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// File picker backed by a hidden &lt;input type="file"&gt; element. Picked file contents
/// are copied into a temp directory so the returned <see cref="FileResult"/> paths are
/// readable with regular System.IO APIs.
/// PickOptions.FileTypes is platform-keyed and has no browser entry, so it is ignored.
/// </summary>
internal sealed class BlazorFilePicker : IFilePicker
{
    public async Task<FileResult?> PickAsync(PickOptions? options = null)
        => (await PickCoreAsync(options, multiple: false)).FirstOrDefault();

    private sealed record PickedFile(string Name, string Type, byte[] data);

    public async Task<IEnumerable<FileResult>?> PickMultipleAsync(PickOptions? options = null)
        => await PickCoreAsync(options, multiple: true);

    private static string? GetAccept(PickOptions? options)
    {
        return null;

        //if (options?.FileTypes?.Value is not { } fileTypes)
        //    return null;

        //// Prefer browser-specific filters if your platform defines them.
        //// Otherwise, collect filters that are meaningful to HTML inputs.
        //var filters = fileTypes
        //    .Where(type => !string.IsNullOrWhiteSpace(type))
        //    .Where(type =>
        //        type.StartsWith('.') ||
        //        type.Contains('/') ||
        //        type.StartsWith('*'))
        //    .Distinct(StringComparer.OrdinalIgnoreCase);

        //var accept = string.Join(",", filters);
        //return string.IsNullOrWhiteSpace(accept) ? null : accept;
    }

    public static Task<IEnumerable<FileResult>> PickCoreAsync(MediaPickerOptions? options, bool multiple)
    {
        PickOptions? pickOptions = null;
        if (options != null)
            pickOptions = new PickOptions()
            {
                PickerTitle = options.Title
                //FileTypes = options.
            };
        return PickCoreAsync(pickOptions, multiple);
    }

    public static async Task<IEnumerable<FileResult>> PickCoreAsync(PickOptions? options, bool multiple)
    {
        try
        {
            var files = await BlazorEssentials.Module.InvokeAsync<PickedFile[]>(
                "pickFiles", options?.PickerTitle, GetAccept(options), multiple);

            var pickDirectory = Path.Combine(
                Path.GetTempPath(), "maui-filepicker", Guid.NewGuid().ToString("N"));

            var results = new List<FileResult>(files.Length);
            foreach (var file in files)
            {
                Directory.CreateDirectory(pickDirectory);
                var path = Path.Combine(pickDirectory, file.Name);
                await File.WriteAllBytesAsync(path, file.data).ConfigureAwait(false);

                var contentType = string.IsNullOrEmpty(file.Type)
                    ? "application/octet-stream"
                    : file.Type;

                results.Add(new BlazorFileResult(path, contentType, file.data));
            }
            return results;
        }
        catch
        {
            return Enumerable.Empty<FileResult>();
        }
    }
}