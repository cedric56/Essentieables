

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel.Communication;
using System;

namespace Essentieables;


/// <summary>Phone dialing via a tel: link (effective on mobile browsers or with a desktop handler).</summary>
internal class BlazorPhoneDialer: IPhoneDialer
{
    public bool IsSupported => true;

    public void Open(string number)
    {
        // Validated synchronously: the exceptions reach the caller, as with MAUI.
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        BlazorEssentials.EnsureInitialized();
        BlazorEssentials.Forget(BlazorLauncher.NavigateToAsync("tel:" + Uri.EscapeDataString(number)), "PhoneDialer.Open");
    }
}
