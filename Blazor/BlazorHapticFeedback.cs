using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Haptic feedback approximated with short navigator.vibrate pulses.</summary>
public class BlazorHapticFeedback : IHapticFeedback
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

    public async void Perform(HapticFeedbackType type = HapticFeedbackType.Click)
    {
        try
        { 
            if (!IsSupported)
                throw new FeatureNotSupportedException("The Vibration API is not available in this browser.");
            await BlazorEssentials.Module.InvokeVoidAsync("vibrate", type == HapticFeedbackType.LongPress ? 25 : 10);
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
