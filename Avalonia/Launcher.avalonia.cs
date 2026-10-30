// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using WinLauncher = Windows.System.Launcher;

namespace Essentieables;

partial class OriginalLauncherImplementation : ILauncher
{
    public Task<bool> CanOpenAsync(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        return PlatformCanOpenAsync(uri);
    }

    public Task<bool> OpenAsync(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        return PlatformOpenAsync(uri);
    }

    public Task<bool> OpenAsync(OpenFileRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (request.File == null)
            throw new ArgumentNullException(nameof(request.File));

        return PlatformOpenAsync(request);
    }

    public Task<bool> TryOpenAsync(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        return PlatformTryOpenAsync(uri);
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalLauncherImplementation
{
    async Task<bool> PlatformCanOpenAsync(Uri uri)
    {
        var supported = await WinLauncher.QueryUriSupportAsync(uri, LaunchQuerySupportType.Uri);
        return supported == LaunchQuerySupportStatus.Available;
    }

    Task<bool> PlatformOpenAsync(Uri uri) =>
        WinLauncher.LaunchUriAsync(uri).AsTask();

    async Task<bool> PlatformOpenAsync(OpenFileRequest request)
    {
        var storageFile = await StorageFile.GetFileFromPathAsync(request?.File?.FullPath);

        return await WinLauncher.LaunchFileAsync(storageFile).AsTask();
    }

    async Task<bool> PlatformTryOpenAsync(Uri uri)
    {
        var canOpen = await PlatformCanOpenAsync(uri);

        if (canOpen)
            return await WinLauncher.LaunchUriAsync(uri).AsTask();

        return canOpen;
    }
}
