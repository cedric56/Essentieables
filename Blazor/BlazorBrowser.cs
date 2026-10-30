// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Opens URIs in a new browser tab via window.open.</summary>
internal class BlazorBrowser : IBrowser
{
    public async Task<bool> OpenAsync(Uri uri, BrowserLaunchOptions options)
    {
        // Launch mode and title/color options have no browser equivalent — always a new tab.
        return await BlazorLauncher.OpenUrlAsync(uri.AbsoluteUri);
    }
}
