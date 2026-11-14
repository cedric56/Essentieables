// macOS only: compiled for net10.0-macos (MACOS is defined by the SDK).
#if MACOS

using AppKit;
using AVFoundation;
using AVKit;
using Avalonia.Controls.Maui.Essentials;
using CoreGraphics;
using CoreMedia;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using ObjCRuntime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// macOS media picker: picking goes through Avalonia, capturing opens a camera window (AVFoundation), as in
/// Avae.macOS.Essentials. The app needs <c>NSCameraUsageDescription</c> (and <c>NSMicrophoneUsageDescription</c> for videos) in
/// its Info.plist, and the <c>com.apple.security.device.camera</c> / <c>.audio-input</c> entitlements when sandboxed.
/// Returns null when the user closes the window.
/// </summary>
internal sealed class AvaeMacMediaPicker(AvaloniaMediaPicker picker) : IAvaeMediaPicker
{
    public bool IsCaptureSupported => AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video) is not null;

    public Task<FileResult?> CaptureAsync(bool isPhoto, MediaPickerOptions? options = null)
        => isPhoto ? CapturePhotoAsync(options) : CaptureVideoAsync(options);

    public async Task<FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
    {
        await MacCamera.EnsureAccessAsync(AVAuthorizationMediaType.Video).ConfigureAwait(false);
        return await OnMainThread(() => new CameraPhotoWindow(options?.Title).ShowAsync()).ConfigureAwait(false);
    }

    public async Task<FileResult?> CaptureVideoAsync(MediaPickerOptions? options = null)
    {
        await MacCamera.EnsureAccessAsync(AVAuthorizationMediaType.Video).ConfigureAwait(false);
        return await OnMainThread(() => new CameraVideoWindow(options?.Title).ShowAsync()).ConfigureAwait(false);
    }

    public Task<FileResult?> PickPhotoAsync(MediaPickerOptions? options = null) => picker.PickPhotoAsync(options);
    public Task<List<FileResult>> PickPhotosAsync(MediaPickerOptions? options = null) => picker.PickPhotosAsync(options);
    public Task<FileResult?> PickVideoAsync(MediaPickerOptions? options = null) => picker.PickVideoAsync(options);
    public Task<List<FileResult>> PickVideosAsync(MediaPickerOptions? options = null) => picker.PickVideosAsync(options);

    static Task<FileResult?> OnMainThread(Func<Task<FileResult?>> show)
        => AvaeMainThread.InvokeOnMainThreadAsync(show);
}

/// <summary>A captured file: readable with <c>Essentieables.Extensions.OpenReadAsync</c> (the portable MAUI build cannot open it).</summary>
internal sealed class MacFileResult(string fullPath, string contentType) : FileResult(fullPath, contentType), IAvaeFileResult
{
    public Task<Stream> OpenFileStreamAsync() => Task.FromResult<Stream>(File.OpenRead(FullPath));
}

static class MacCamera
{
    /// <summary>Asks for the camera (or microphone) access; throws <see cref="PermissionException"/> when denied.</summary>
    public static async Task EnsureAccessAsync(AVAuthorizationMediaType mediaType)
    {
        switch (AVCaptureDevice.GetAuthorizationStatus(mediaType))
        {
            case AVAuthorizationStatus.Authorized:
                return;
            case AVAuthorizationStatus.NotDetermined:
                if (await AVCaptureDevice.RequestAccessForMediaTypeAsync(mediaType).ConfigureAwait(false))
                    return;
                break;
        }
        throw new PermissionException($"{(mediaType == AVAuthorizationMediaType.Video ? "Camera" : "Microphone")} access was denied. Enable it in System Settings > Privacy & Security.");
    }

    /// <summary>The window of the app the camera window is attached to.</summary>
    public static NSWindow? GetParentWindow()
    {
        var handle = AvaeTopLevelStateManager.Current?.GetTopLevel()?.TryGetPlatformHandle();
        if (handle is not null && handle.Handle != IntPtr.Zero)
        {
            if (handle.HandleDescriptor == "NSWindow")
                return Runtime.GetNSObject<NSWindow>(handle.Handle);
            if (handle.HandleDescriptor == "NSView")
                return Runtime.GetNSObject<NSView>(handle.Handle)?.Window;
        }
        return NSApplication.SharedApplication.KeyWindow ?? NSApplication.SharedApplication.MainWindow;
    }

    public static NSWindow CreateWindow(string title, double width, double height)
    {
        var window = new NSWindow(
            new CGRect(0, 0, width, height),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            false)
        {
            Title = title,
            ReleasedWhenClosed = false,
        };

        GetParentWindow()?.AddChildWindow(window, NSWindowOrderingMode.Above);
        window.Level = NSWindowLevel.Floating;
        window.Center();
        return window;
    }

    public static NSButton Button(string title, CGRect frame) => new() { Title = title, Frame = frame };

    public static string CaptureDirectory()
    {
        var directory = Path.Combine(FileSystem.CacheDirectory, "capture");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

// ───────────────────────── Photo ─────────────────────────

sealed class CameraPhotoWindow(string? title)
{
    readonly TaskCompletionSource<FileResult?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    NSWindow? _window;
    NSView? _container;
    CameraPhotoView? _cameraView;
    NSButton? _captureButton;

    public Task<FileResult?> ShowAsync()
    {
        _window = MacCamera.CreateWindow(string.IsNullOrEmpty(title) ? "Camera" : title, 600, 400);
        _window.WillClose += (_, _) =>
        {
            _cameraView?.Dispose();
            _tcs.TrySetResult(null);
        };

        _container = new NSView(new CGRect(0, 0, 600, 400));
        _captureButton = MacCamera.Button("Capture", new CGRect(250, 10, 100, 30));
        _captureButton.Activated += async (_, _) =>
        {
            if (_cameraView is null)
                return;
            _captureButton.Hidden = true;
            try
            {
                var image = await _cameraView.TakeSnapshotAsync();
                if (image is not null)
                    ShowPreview(image);
                else
                    _captureButton.Hidden = false;
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                _window?.Close();
            }
        };

        ShowCamera();
        _window.ContentView = _container;
        _window.MakeKeyAndOrderFront(null);
        return _tcs.Task;
    }

    void ShowCamera()
    {
        _cameraView = new CameraPhotoView();
        _cameraView.View.Frame = new CGRect(0, 50, 600, 350);
        _container!.AddSubview(_cameraView.View);
        _captureButton!.Hidden = false;
        _container.AddSubview(_captureButton);
    }

    void ShowPreview(NSImage image)
    {
        _cameraView?.View.RemoveFromSuperview();
        _cameraView?.Dispose();
        _cameraView = null;

        var imageView = new NSImageView(new CGRect(0, 50, 600, 350))
        {
            Image = image,
            ImageScaling = NSImageScale.ProportionallyUpOrDown,
        };
        _container!.AddSubview(imageView);

        var accept = MacCamera.Button("Accept", new CGRect(150, 10, 100, 30));
        var retake = MacCamera.Button("Retake", new CGRect(350, 10, 100, 30));

        accept.Activated += (_, _) =>
        {
            try
            {
                _tcs.TrySetResult(new MacFileResult(SaveJpeg(image), "image/jpeg"));
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
            _window?.Close();
        };

        retake.Activated += (_, _) =>
        {
            imageView.RemoveFromSuperview();
            accept.RemoveFromSuperview();
            retake.RemoveFromSuperview();
            ShowCamera();
        };

        _container.AddSubview(accept);
        _container.AddSubview(retake);
    }

    static string SaveJpeg(NSImage image)
    {
        var path = Path.Combine(MacCamera.CaptureDirectory(), $"photo_{Guid.NewGuid():N}.jpg");
        using var tiff = image.AsTiff() ?? throw new InvalidOperationException("The captured image cannot be encoded.");
        using var rep = new NSBitmapImageRep(tiff);
        using var jpeg = rep.RepresentationUsingTypeProperties(NSBitmapImageFileType.Jpeg, new NSDictionary())
            ?? throw new InvalidOperationException("The captured image cannot be encoded as JPEG.");
        if (!jpeg.Save(NSUrl.FromFilename(path), true))
            throw new IOException($"Unable to write '{path}'.");
        return path;
    }
}

sealed class CameraPhotoView : NSViewController, IAVCapturePhotoCaptureDelegate
{
    AVCaptureSession? _session;
    AVCaptureDeviceInput? _input;
    AVCaptureVideoPreviewLayer? _previewLayer;
    AVCapturePhotoOutput? _photoOutput;
    TaskCompletionSource<NSImage?>? _snapshot;

    // No nib: the view is created in code.
    public override void LoadView() => View = new NSView(new CGRect(0, 0, 600, 350));

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.WantsLayer = true;

        var device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
        if (device is null)
        {
            View.AddSubview(MacCameraLabels.Message("No camera found.", View.Bounds));
            return;
        }

        _input = AVCaptureDeviceInput.FromDevice(device, out var error);
        if (_input is null || error is not null)
        {
            View.AddSubview(MacCameraLabels.Message(error?.LocalizedDescription ?? "The camera cannot be opened.", View.Bounds));
            return;
        }

        _session = new AVCaptureSession { SessionPreset = AVCaptureSession.PresetPhoto };
        _session.AddInput(_input);

        _previewLayer = new AVCaptureVideoPreviewLayer(_session)
        {
            Frame = View.Bounds,
            VideoGravity = AVLayerVideoGravity.ResizeAspectFill,
        };
        View.Layer = _previewLayer;

        _photoOutput = new AVCapturePhotoOutput();
        _session.AddOutput(_photoOutput);
        _session.StartRunning();
    }

    public Task<NSImage?> TakeSnapshotAsync()
    {
        if (_photoOutput is null)
            return Task.FromResult<NSImage?>(null);

        _snapshot = new TaskCompletionSource<NSImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var settings = AVCapturePhotoSettings.Create();
        _photoOutput.CapturePhoto(settings, this);
        return _snapshot.Task;
    }

    [Export("captureOutput:didFinishProcessingPhoto:error:")]
    public void DidFinishProcessingPhoto(AVCapturePhotoOutput captureOutput, AVCapturePhoto photo, NSError? error)
    {
        if (error is not null)
        {
            _snapshot?.TrySetException(new NSErrorException(error));
            return;
        }

        var data = photo.FileDataRepresentation;
        if (data is null)
            _snapshot?.TrySetException(new InvalidOperationException("No image data captured."));
        else
            _snapshot?.TrySetResult(new NSImage(data));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session?.StopRunning();
            _session?.Dispose();
            _input?.Dispose();
            _previewLayer?.Dispose();
            _photoOutput?.Dispose();
            _session = null;
        }
        base.Dispose(disposing);
    }
}

// ───────────────────────── Video ─────────────────────────

sealed class CameraVideoWindow(string? title)
{
    readonly TaskCompletionSource<FileResult?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    NSWindow? _window;
    NSView? _container;
    CameraVideoView? _videoView;
    NSButton? _start;
    NSButton? _stop;
    string? _recordedPath;
    AVPlayer? _player;

    public Task<FileResult?> ShowAsync()
    {
        _window = MacCamera.CreateWindow(string.IsNullOrEmpty(title) ? "Camera" : title, 640, 520);
        _window.WillClose += (_, _) =>
        {
            _player?.Pause();
            _videoView?.Dispose();
            if (!_tcs.Task.IsCompleted && _recordedPath is not null)
                TryDelete(_recordedPath);
            _tcs.TrySetResult(null);
        };

        _container = new NSView(new CGRect(0, 0, 640, 520));
        _start = MacCamera.Button("Record", new CGRect(270, 15, 100, 30));
        _stop = MacCamera.Button("Stop", new CGRect(270, 15, 100, 30));
        _stop.Hidden = true;

        _start.Activated += async (_, _) =>
        {
            if (_videoView is null)
                return;
            try
            {
                _start.Hidden = true;
                _stop.Hidden = false;
                _recordedPath = await _videoView.StartRecordingAsync(MacCamera.CaptureDirectory());
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                _window?.Close();
            }
        };

        _stop.Activated += async (_, _) =>
        {
            if (_videoView is null)
                return;
            _stop.Hidden = true;
            try
            {
                var path = await _videoView.StopRecordingAsync();
                if (path is not null)
                    ShowPlayback(path);
                else
                    _start.Hidden = false;
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                _window?.Close();
            }
        };

        ShowCamera();
        _window.ContentView = _container;
        _window.MakeKeyAndOrderFront(null);
        return _tcs.Task;
    }

    void ShowCamera()
    {
        _videoView = new CameraVideoView();
        _videoView.View.Frame = new CGRect(0, 60, 640, 460);
        _container!.AddSubview(_videoView.View);
        _container.AddSubview(_start!);
        _container.AddSubview(_stop!);
        _start!.Hidden = false;
        _stop!.Hidden = true;
    }

    void ShowPlayback(string path)
    {
        _videoView?.View.RemoveFromSuperview();
        _videoView?.Dispose();
        _videoView = null;

        _player = new AVPlayer(NSUrl.FromFilename(path));
        var playerView = new AVPlayerView(new CGRect(0, 60, 640, 460))
        {
            Player = _player,
            ControlsStyle = AVPlayerViewControlsStyle.Inline,
        };
        _container!.AddSubview(playerView);

        var accept = MacCamera.Button("Accept", new CGRect(400, 15, 100, 30));
        var retake = MacCamera.Button("Retake", new CGRect(140, 15, 100, 30));

        accept.Activated += (_, _) =>
        {
            _player?.Pause();
            _tcs.TrySetResult(new MacFileResult(path, "video/quicktime"));
            _window?.Close();
        };

        retake.Activated += (_, _) =>
        {
            _player?.Pause();
            _player = null;
            TryDelete(path);
            _recordedPath = null;
            foreach (var view in _container.Subviews.ToList())
                view.RemoveFromSuperview();
            ShowCamera();
        };

        _container.AddSubview(accept);
        _container.AddSubview(retake);
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}

sealed class CameraVideoView : NSViewController
{
    AVCaptureSession? _session;
    AVCaptureDeviceInput? _videoInput;
    AVCaptureDeviceInput? _audioInput;
    AVCaptureMovieFileOutput? _movieOutput;
    AVCaptureVideoPreviewLayer? _previewLayer;
    RecordingDelegate? _recording;

    public override void LoadView() => View = new NSView(new CGRect(0, 0, 640, 460));

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.WantsLayer = true;

        var videoDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
        if (videoDevice is null)
        {
            View.AddSubview(MacCameraLabels.Message("No camera found.", View.Bounds));
            return;
        }

        _videoInput = AVCaptureDeviceInput.FromDevice(videoDevice, out var error);
        if (_videoInput is null || error is not null)
        {
            View.AddSubview(MacCameraLabels.Message(error?.LocalizedDescription ?? "The camera cannot be opened.", View.Bounds));
            return;
        }

        _session = new AVCaptureSession { SessionPreset = AVCaptureSession.PresetHigh };
        _session.AddInput(_videoInput);

        // Sound is optional: no microphone or no access, the video is silent.
        if (AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio) != AVAuthorizationStatus.Denied
            && AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Audio) is { } audioDevice)
        {
            _audioInput = AVCaptureDeviceInput.FromDevice(audioDevice, out _);
            if (_audioInput is not null && _session.CanAddInput(_audioInput))
                _session.AddInput(_audioInput);
        }

        _previewLayer = new AVCaptureVideoPreviewLayer(_session)
        {
            Frame = View.Bounds,
            VideoGravity = AVLayerVideoGravity.ResizeAspectFill,
        };
        View.Layer = _previewLayer;

        _movieOutput = new AVCaptureMovieFileOutput();
        _session.AddOutput(_movieOutput);
        _session.StartRunning();
    }

    public Task<string?> StartRecordingAsync(string directory)
    {
        if (_movieOutput is null)
            return Task.FromResult<string?>(null);

        var path = Path.Combine(directory, $"video_{Guid.NewGuid():N}.mov");
        _recording = new RecordingDelegate();
        _movieOutput.StartRecordingToOutputFile(NSUrl.FromFilename(path), _recording);
        return _recording.Started.Task;
    }

    /// <summary>Completes with the path of the movie once it is written.</summary>
    public Task<string?> StopRecordingAsync()
    {
        if (_movieOutput is null || _recording is null)
            return Task.FromResult<string?>(null);

        _movieOutput.StopRecording();
        return _recording.Finished.Task;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_movieOutput?.Recording == true)
                _movieOutput.StopRecording();
            _session?.StopRunning();
            _session?.Dispose();
            _videoInput?.Dispose();
            _audioInput?.Dispose();
            _previewLayer?.Dispose();
            _movieOutput?.Dispose();
            _session = null;
        }
        base.Dispose(disposing);
    }

    sealed class RecordingDelegate : AVCaptureFileOutputRecordingDelegate
    {
        public TaskCompletionSource<string?> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void DidStartRecording(AVCaptureFileOutput captureOutput, NSUrl outputFileUrl, NSObject[] connections)
            => Started.TrySetResult(outputFileUrl.Path);

        public override void FinishedRecording(AVCaptureFileOutput captureOutput, NSUrl outputFileUrl, NSObject[] connections, NSError? error)
        {
            // An error can still leave a usable file (maximum duration or disk space reached…).
            var finished = error is null || File.Exists(outputFileUrl.Path);
            if (finished)
                Finished.TrySetResult(outputFileUrl.Path);
            else
                Finished.TrySetException(new NSErrorException(error!));
            Started.TrySetResult(outputFileUrl.Path);
        }
    }
}

static class MacCameraLabels
{
    public static NSTextField Message(string text, CGRect bounds)
    {
        const double width = 420, height = 24;
        return new NSTextField(new CGRect(((double)bounds.Width - width) / 2, ((double)bounds.Height - height) / 2, width, height))
        {
            StringValue = text,
            Editable = false,
            Bordered = false,
            DrawsBackground = false,
            Alignment = NSTextAlignment.Center,
            Font = NSFont.SystemFontOfSize(14),
        };
    }
}
#endif
