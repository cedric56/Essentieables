using Microsoft.JSInterop;
using Microsoft.Maui.Devices;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>The camera torch is not reachable from the browser.</summary>
internal class BlazorFlashlight : IFlashlight
{
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            return await BlazorEssentials.Module.InvokeAsync<bool>("isTorchSupported");
        }
        catch
        {
            throw;
        }
    }

    public async Task TurnOnAsync()
    {
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("on");
        }
        catch
        {
            throw;
        }
    }

    public async Task TurnOffAsync()
    {
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("off");
        }
        catch
        {
            throw;
        }
    }
}
