// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

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

    public async Task SetTextAsync(string? text)
    {
        try
        { 
        await BlazorEssentials.Module.InvokeVoidAsync("clipboardWriteText", text ?? string.Empty).ConfigureAwait(false);
        lastKnownHasText = !string.IsNullOrEmpty(text);
        ClipboardContentChanged?.Invoke(this, EventArgs.Empty);
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

    public async Task<string?> GetTextAsync()
    {
        try
        { 
            var text = await BlazorEssentials.Module.InvokeAsync<string?>("clipboardReadText").ConfigureAwait(false);
            lastKnownHasText = !string.IsNullOrEmpty(text);
            return string.IsNullOrEmpty(text) ? null : text;
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return string.Empty;
        }
#else
        catch
        {
            return string.Empty;
        }
#endif
    }
}
