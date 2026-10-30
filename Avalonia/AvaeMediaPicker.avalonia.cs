// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Essentieables;
using Avalonia.Controls.Maui.Essentials;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.System;
using WinRT;
using Launcher = Windows.System.Launcher;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
internal class AvaeMediaPicker(AvaeTopLevelStateManager topLevelStateManager, AvaloniaMediaPicker picker) : IMediaPicker
{
    public class AvaeFileResult(string fullPath, string contentType) : FileResult(fullPath, contentType), IAvaeFileResult
    {
        public async Task<Stream> OpenFileStreamAsync()
        {
            var file = await StorageFile.GetFileFromPathAsync(FullPath);
            return await file.OpenStreamForReadAsync();
        }
    }

    [ComImport]
    [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // WinRT interfaces are IInspectable-based
    interface IInitializeWithWindow
    {
        [PreserveSig] int Initialize(IntPtr hwnd);
    }

    public bool IsCaptureSupported => true;

    public async Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
    {
        var captureUi = new WinUICameraCaptureUI(topLevelStateManager);
        captureUi.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
        var file = await captureUi.CaptureFileAsync(CameraCaptureUIMode.Photo);

        if (file is not null)
#if WINDOWS
            return new FileResult(file.Path);
#else
            return new AvaeFileResult(file.Path, ContentTypeResolver.Resolve(file.Path));
#endif
        return null;
    }

    public async Task<FileResult?> CaptureVideoAsync(MediaPickerOptions? options = null)
    {
        var captureUi = new WinUICameraCaptureUI(topLevelStateManager);
        captureUi.VideoSettings.Format = CameraCaptureUIVideoFormat.Mp4;
        var file = await captureUi.CaptureFileAsync(CameraCaptureUIMode.Video);
        if (file is not null)
#if WINDOWS
            return new FileResult(file.Path);
#else
            return new AvaeFileResult(file.Path, ContentTypeResolver.Resolve(file.Path));
#endif
        return null;
    }

    public Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null)
    {
        return picker.PickPhotoAsync(options);
    }

    public Task<List<FileResult>> PickPhotosAsync(MediaPickerOptions? options = null)
    {
        return picker.PickPhotosAsync(options);
    }

    public Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null)
    {
        return picker.PickVideoAsync(options);
    }

    public Task<List<FileResult>> PickVideosAsync(MediaPickerOptions? options = null)
    {
        return picker.PickVideosAsync(options);
    }

    class WinUICameraCaptureUI(AvaeTopLevelStateManager topLevelStateManager)
    {
        const string WindowsCameraAppPackageName = "Microsoft.WindowsCamera_8wekyb3d8bbwe";
        const string WindowsCameraAppUri = "microsoft.windows.camera.picker:";

        const string CacheFolderName = ".Microsoft.Maui.Media.MediaPicker";
        const string CacheFileName = "capture";

        public WinUICameraCaptureUIPhotoCaptureSettings PhotoSettings { get; } = new();

        public WinUICameraCaptureUIVideoCaptureSettings VideoSettings { get; } = new();

        public async Task<StorageFile?> CaptureFileAsync(CameraCaptureUIMode mode)
        {
            var hwnd = topLevelStateManager.GetActiveWindowHandle(false);

            var options = new LauncherOptions();
            options.As<IInitializeWithWindow>().Initialize(hwnd);

            options.TreatAsUntrusted = false;
            options.DisplayApplicationPicker = false;
            options.TargetApplicationPackageFamilyName = WindowsCameraAppPackageName;

            var extension = mode == CameraCaptureUIMode.Photo
                ? PhotoSettings.GetFormatExtension()
                : VideoSettings.GetFormatExtension();

            var tempLocation = await StorageFolder.GetFolderFromPathAsync(FileSystem.CacheDirectory);
            var tempFolder = await tempLocation.CreateFolderAsync(CacheFolderName, CreationCollisionOption.OpenIfExists);
            var tempFile = await tempFolder.CreateFileAsync($"{CacheFileName}{extension}", CreationCollisionOption.GenerateUniqueName);
            var token = global::Windows.ApplicationModel.DataTransfer.SharedStorageAccessManager.AddFile(tempFile);

            var set = new ValueSet();
            if (mode == CameraCaptureUIMode.Photo)
            {
                set.Add("MediaType", "photo");
                set.Add("PhotoFileToken", token);
            }
            else
            {
                set.Add("MediaType", "video");
                set.Add("VideoFileToken", token);
            }

            var uri = new Uri(WindowsCameraAppUri);
            var result = await Launcher.LaunchUriForResultsAsync(uri, options, set);

            global::Windows.ApplicationModel.DataTransfer.SharedStorageAccessManager.RemoveFile(token);

            if (result.Status == LaunchUriStatus.Success && result.Result is not null)
                return tempFile;

            return null;
        }
    }

    [SupportedOSPlatform("windows10.0.10240")]
    class WinUICameraCaptureUIPhotoCaptureSettings
    {
        public CameraCaptureUIPhotoFormat Format { get; set; }

        public string GetFormatExtension() =>
            Format switch
            {
                CameraCaptureUIPhotoFormat.Jpeg => ".jpg",
                CameraCaptureUIPhotoFormat.Png => ".png",
                CameraCaptureUIPhotoFormat.JpegXR => ".jpg",
                _ => ".jpg",
            };
    }

    [SupportedOSPlatform("windows10.0.10240")]
    class WinUICameraCaptureUIVideoCaptureSettings
    {
        public CameraCaptureUIVideoFormat Format { get; set; }

        public string GetFormatExtension() =>
            Format switch
            {
                CameraCaptureUIVideoFormat.Mp4 => ".mp4",
                CameraCaptureUIVideoFormat.Wmv => ".wmv",
                _ => ".mp4",
            };
    }
}
