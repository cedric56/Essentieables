using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Picking uses the file picker (image/* or video/*). Capturing opens a camera popup
/// (getUserMedia, MediaRecorder for video); the result is written to the session cache, so the
/// returned <see cref="FileResult"/> has a real path and stays readable after the capture.
/// </summary>
internal class BlazorMediaPicker : IAvaeMediaPicker
{
    internal sealed record CaptureResult(string Url, string? Type);

    bool _isCaptureSupported;

    /// <summary>False when the camera API is missing or the page is not a secure context (HTTPS).</summary>
    public bool IsCaptureSupported => _isCaptureSupported;

    internal async Task InitializeAsync()
    {
        try
        {
            _isCaptureSupported = await BlazorEssentials.Module.InvokeAsync<bool>("captureIsSupported").ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            _isCaptureSupported = false;
        }
    }

    public Task<FileResult?> CaptureAsync(bool isPhoto, MediaPickerOptions? options = null)
        => isPhoto ? CapturePhotoAsync(options) : CaptureVideoAsync(options);

    public async Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null)
        => (await BlazorFilePicker.PickCoreAsync(options, multiple: false, "image/*").ConfigureAwait(false)).FirstOrDefault();

    public async Task<List<FileResult>> PickPhotosAsync(MediaPickerOptions? options = null)
        => (await BlazorFilePicker.PickCoreAsync(options, multiple: true, "image/*").ConfigureAwait(false)).ToList();

    public async Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null)
        => (await BlazorFilePicker.PickCoreAsync(options, multiple: false, "video/*").ConfigureAwait(false)).FirstOrDefault();

    public async Task<List<FileResult>> PickVideosAsync(MediaPickerOptions? options = null)
        => (await BlazorFilePicker.PickCoreAsync(options, multiple: true, "video/*").ConfigureAwait(false)).ToList();

    public Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
        => CaptureCoreAsync("capturePhotoInPopup", "image/png");

    public Task<FileResult?> CaptureVideoAsync(MediaPickerOptions? options = null)
        => CaptureCoreAsync("captureVideoInPopup", "video/webm");

    /// <summary>Returns null when the user dismisses the popup.</summary>
    static async Task<FileResult?> CaptureCoreAsync(string function, string defaultType)
    {
        var module = BlazorEssentials.Module;

        CaptureResult? capture;
        try
        {
            capture = await module.InvokeAsync<CaptureResult?>(function).ConfigureAwait(false);
        }
        catch (JSException ex) when (ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            throw new PermissionException("Camera access was denied by the user or the browser policy.");
        }
        catch (JSException ex) when (ex.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotSupportedException("Camera capture is not available in this browser or context (HTTPS is required).");
        }

        if (capture is null || string.IsNullOrWhiteSpace(capture.Url))
            return null;

        var contentType = string.IsNullOrWhiteSpace(capture.Type) ? defaultType : capture.Type;
        var baseType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        var extension = baseType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            _ => baseType.StartsWith("video/", StringComparison.Ordinal) ? ".webm" : ".png",
        };

        var directory = Path.Combine(BlazorFileSystem.GetCacheDirectory(), "capture");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{extension}");

        try
        {
            // takeBlob revokes the blob: URL once it is read; the file on disk is what the result points to.
            await using var data = await module.InvokeAsync<IJSStreamReference>("takeBlob", capture.Url).ConfigureAwait(false);
            await using var source = await data.OpenReadStreamAsync(BlazorFilePicker.MaxFileSize).ConfigureAwait(false);
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await source.CopyToAsync(target).ConfigureAwait(false);
        }
        catch
        {
            try { File.Delete(path); } catch (IOException) { }
            throw;
        }

        return new BlazorFileResult(path, baseType);
    }
}
