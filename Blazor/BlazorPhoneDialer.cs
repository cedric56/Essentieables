// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel.Communication;
using System;

namespace Essentieables;


/// <summary>Phone dialing via a tel: link (effective on mobile browsers or with a desktop handler).</summary>
internal class BlazorPhoneDialer: IPhoneDialer
{
    public bool IsSupported => true;

    public async void Open(string number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        BlazorEssentials.EnsureInitialized();
        await BlazorLauncher.NavigateToAsync("tel:" + Uri.EscapeDataString(number));
    }
}
