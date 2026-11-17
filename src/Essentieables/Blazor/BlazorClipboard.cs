

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Clipboard backed by the async Clipboard API (navigator.clipboard).
/// Browsers gate clipboard reads behind a user permission prompt, and there is no
/// synchronous "has text" query — <see cref="HasText"/> reflects the last value
/// observed through this API rather than the live OS clipboard.
/// </summary>
internal class BlazorClipboard : IClipboard
{
    bool lastKnownHasText;

    public bool HasText => lastKnownHasText;

    public event EventHandler<EventArgs>? ClipboardContentChanged;

    /// <summary>Throws <see cref="PermissionException"/> when the browser refuses (permission, no user gesture).</summary>
    public async Task SetTextAsync(string? text)
    {
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("clipboardWriteText", text ?? string.Empty).ConfigureAwait(false);
        }
        catch (JSException ex) when (BlazorEssentials.MapJSException(ex, "The clipboard") is { } mapped)
        {
            throw mapped;
        }
        lastKnownHasText = !string.IsNullOrEmpty(text);
        ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The text of the clipboard, null when it has none (as with MAUI). Throws <see cref="PermissionException"/> when the
    /// browser refuses to read it.
    /// </summary>
    public async Task<string?> GetTextAsync()
    {
        string? text;
        try
        {
            text = await BlazorEssentials.Module.InvokeAsync<string?>("clipboardReadText").ConfigureAwait(false);
        }
        catch (JSException ex) when (BlazorEssentials.MapJSException(ex, "The clipboard") is { } mapped)
        {
            throw mapped;
        }
        lastKnownHasText = !string.IsNullOrEmpty(text);
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
