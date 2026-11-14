

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Vibration backed by navigator.vibrate (mobile browsers; no-ops when unsupported by hardware).</summary>
internal class BlazorVibration : IVibration
{
    private bool isSupported;

    public async Task InitializeAsync()
    {
        try
        {
            isSupported = await BlazorEssentials.Module.InvokeAsync<bool>("vibrationIsSupported");
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

    public bool IsSupported
    {
        get
        {
            BlazorEssentials.EnsureInitialized();
            return isSupported;
        }
    }

    public void Vibrate() => Vibrate(TimeSpan.FromMilliseconds(500));

    public void Vibrate(TimeSpan duration)
    {
        // Validated synchronously: FeatureNotSupportedException reaches the caller, as with MAUI.
        if (!IsSupported)
            throw new FeatureNotSupportedException("The Vibration API is not available in this browser.");
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        BlazorEssentials.Forget(BlazorEssentials.Module.InvokeVoidAsync("vibrate", duration.TotalMilliseconds).AsTask(), "Vibration.Vibrate");
    }

    public void Cancel()
    {
        if (IsSupported)
            BlazorEssentials.Forget(BlazorEssentials.Module.InvokeVoidAsync("vibrate", 0).AsTask(), "Vibration.Cancel");
    }
}
