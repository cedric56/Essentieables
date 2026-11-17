using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// The torch of the camera (MediaStreamTrack "torch" constraint): Chrome on Android, with HTTPS. Elsewhere, and on cameras
/// without a lamp, <see cref="IsSupportedAsync"/> returns false and <see cref="TurnOnAsync"/> throws
/// <see cref="FeatureNotSupportedException"/>.
/// </summary>
internal class BlazorFlashlight : IFlashlight
{
    public async Task<bool> IsSupportedAsync()
        => await BlazorEssentials.Module.InvokeAsync<bool>("isTorchSupported");

    public async Task TurnOnAsync()
    {
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("on");
        }
        catch (JSException ex) when (ex.Message.Contains("TorchNotSupported", System.StringComparison.Ordinal)
                                     || ex.Message.Contains("not supported", System.StringComparison.OrdinalIgnoreCase)
                                     || ex.Message.Contains("no flashlight", System.StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotSupportedException(ex.Message, ex);
        }
    }

    public async Task TurnOffAsync()
        => await BlazorEssentials.Module.InvokeVoidAsync("off");
}
