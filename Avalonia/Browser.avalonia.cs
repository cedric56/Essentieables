// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

#nullable enable
using Microsoft.Maui.ApplicationModel;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalBrowserImplementation : IBrowser
{
    public Task<bool> OpenAsync(Uri uri, BrowserLaunchOptions options) =>
        global::Windows.System.Launcher.LaunchUriAsync(uri).AsTask();
}
