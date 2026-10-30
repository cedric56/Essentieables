using Microsoft.JSInterop;
using Microsoft.Maui.Accessibility;
using System;

namespace Essentieables;

/// <summary>Screen reader announcements via a visually-hidden aria-live region.</summary>
internal class BlazorSemanticScreenReader : ISemanticScreenReader
{
    public async void Announce(string text)
    {
        try
        {
            BlazorEssentials.EnsureInitialized();
            await BlazorEssentials.Module.InvokeVoidAsync("announce", text);
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
}
