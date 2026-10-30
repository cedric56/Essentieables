// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace Essentieables;

internal class BlazorMediaPicker : IAvaeMediaPicker
{
    public bool IsCaptureSupported => true;

    public Task<FileResult?> CaptureAsync(bool isPhoto, MediaPickerOptions? options = null)
    => isPhoto ? CapturePhotoAsync(options) : CaptureVideoAsync(options);

    public async Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null)
        => (await BlazorFilePicker.PickCoreAsync(options, multiple: false)).FirstOrDefault();

    public async Task<List<FileResult>> PickPhotosAsync(MediaPickerOptions? options = null)
    => (await BlazorFilePicker.PickCoreAsync(options,
        multiple: true) ?? []).ToList();

    public async Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null)
    => (await BlazorFilePicker.PickCoreAsync(options,
        multiple: false)).FirstOrDefault();

    public async Task<List<FileResult>> PickVideosAsync(MediaPickerOptions? options = null)
    => (await BlazorFilePicker.PickCoreAsync(options,
        multiple: true) ?? []).ToList();

    public async Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
    {
        try
        { 
            var url = await BlazorEssentials.Module.InvokeAsync<string>("capturePhotoInPopup");
            if (!string.IsNullOrWhiteSpace(url))
            {
                byte[] bytes = await BlazorEssentials.Module.InvokeAsync<byte[]>("getBlobBytes", url);
                return new BlazorFileResult(url, ContentTypeResolver.Resolve(Path.GetFileName(url)), bytes);
            }
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
        return null;
    }

    public async Task<FileResult?> CaptureVideoAsync(MediaPickerOptions? options = null)
    {
        try
        { 
            var url = await BlazorEssentials.Module.InvokeAsync<string>("captureVideoInPopup");
            if (!string.IsNullOrWhiteSpace(url))
            {
                byte[] bytes = await BlazorEssentials.Module.InvokeAsync<byte[]>("getBlobBytes", url);
                return new BlazorFileResult(url, ContentTypeResolver.Resolve(Path.GetFileName(url)), bytes);
            }
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
        return null;
    }
}
