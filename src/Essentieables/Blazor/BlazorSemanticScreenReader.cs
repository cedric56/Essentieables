

using Microsoft.JSInterop;
using Microsoft.Maui.Accessibility;
using System;

namespace Essentieables;

/// <summary>Screen reader announcements via a visually-hidden aria-live region.</summary>
internal class BlazorSemanticScreenReader : ISemanticScreenReader
{
    public void Announce(string text)
    {
        BlazorEssentials.EnsureInitialized();
        BlazorEssentials.Forget(BlazorEssentials.Module.InvokeVoidAsync("announce", text).AsTask(), "SemanticScreenReader.Announce");
    }
}
