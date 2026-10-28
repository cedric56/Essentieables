#nullable enable
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using DataPackage = Windows.ApplicationModel.DataTransfer.DataPackage;

using WindowsClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Essentieables;

partial class OriginalClipboardImplementation : IClipboard
{
    event EventHandler<EventArgs>? ClipboardContentChangedInternal;

    public event EventHandler<EventArgs> ClipboardContentChanged
    {
        add
        {
            if (ClipboardContentChangedInternal == null)
                StartClipboardListeners();
            ClipboardContentChangedInternal += value;
        }
        remove
        {
            ClipboardContentChangedInternal -= value;
            if (ClipboardContentChangedInternal == null)
                StopClipboardListeners();
        }
    }

    internal void OnClipboardContentChanged() =>
        ClipboardContentChangedInternal?.Invoke(this, EventArgs.Empty);
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalClipboardImplementation : IClipboard
{
    public Task SetTextAsync(string? text)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        WindowsClipboard.SetContent(dataPackage);
        return Task.CompletedTask;
    }

    public bool HasText
        => WindowsClipboard.GetContent().Contains(StandardDataFormats.Text);

    public Task<string?> GetTextAsync()
    {
        var clipboardContent = WindowsClipboard.GetContent();
        return clipboardContent.Contains(StandardDataFormats.Text)
            ? clipboardContent.GetTextAsync().AsTask()
            : Task.FromResult<string?>(null);
    }

    void StartClipboardListeners()
        => WindowsClipboard.ContentChanged += ClipboardChangedEventListener;

    void StopClipboardListeners()
        => WindowsClipboard.ContentChanged -= ClipboardChangedEventListener;

    /// <summary>
    /// The event listener for triggering the <see cref="ClipboardContentChanged"/> event.
    /// </summary>
    /// <param name="sender">The object that initiated the event.</param>
    /// <param name="val">The value for this event.</param>
    public void ClipboardChangedEventListener(object? sender, object val) => OnClipboardContentChanged();
}
