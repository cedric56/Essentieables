// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

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

    public async void Vibrate(TimeSpan duration)
    {
        try
        {
            BlazorEssentials.EnsureInitialized();
            if (!IsSupported)
                throw new FeatureNotSupportedException("The Vibration API is not available in this browser.");
            await BlazorEssentials.Module.InvokeVoidAsync("vibrate", duration.TotalMilliseconds);
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

    public async void Cancel()
    {
        try
        {
            BlazorEssentials.EnsureInitialized();
            if (IsSupported)
                await BlazorEssentials.Module.InvokeVoidAsync("vibrate", 0);
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
