using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Opens URIs in a new browser tab via window.open.</summary>
public class BlazorBrowser : IBrowser
{
    public async Task<bool> OpenAsync(Uri uri, BrowserLaunchOptions options)
    {
        // Launch mode and title/color options have no browser equivalent — always a new tab.
        return await BlazorLauncher.OpenUrlAsync(uri.AbsoluteUri);
    }
}
