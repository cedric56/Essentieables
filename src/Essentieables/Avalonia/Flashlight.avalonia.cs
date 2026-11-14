// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
class OriginalFlashlightImplementation : IFlashlight
{
    // MAUI used Monitor.Enter/Exit around awaits: the lock leaked on the early return and on exceptions, and Exit could
    // run on another thread (SynchronizationLockException). A SemaphoreSlim can be held across awaits.
    readonly SemaphoreSlim gate = new(1, 1);
    bool hasLoadedLamp;
    Lamp? lamp;

    async Task<Lamp?> GetLampAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (hasLoadedLamp)
                return lamp;

            // find all the lamps, preferably a back one
            var selector = Lamp.GetDeviceSelector();
            var allLamps = await DeviceInformation.FindAllAsync(selector);
            var lampInfo = allLamps.FirstOrDefault(di => di.EnclosureLocation?.Panel == Panel.Back);

            // if there is no back lamp, use the default lamp
            lamp = lampInfo is not null
                ? await Lamp.FromIdAsync(lampInfo.Id)
                : await Lamp.GetDefaultAsync();

            hasLoadedLamp = true;
            return lamp;
        }
        finally
        {
            gate.Release();
        }
    }

    // Called with the gate held.
    async Task<Lamp?> ReloadLampAsync()
    {
        lamp = await Lamp.GetDefaultAsync();
        hasLoadedLamp = true;
        return lamp;
    }

    /// <summary>
    /// Checks if the flashlight is available and can be turned on or off.
    /// </summary>
    /// <returns><see langword="true"/> when the flashlight is available, or <see langword="false"/> when not</returns>
    public async Task<bool> IsSupportedAsync() => await GetLampAsync().ConfigureAwait(false) is not null;

    public async Task TurnOnAsync()
    {
        if (await GetLampAsync().ConfigureAwait(false) is null)
            throw new FeatureNotSupportedException();

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // TurnOffAsync may have released it meanwhile.
            var current = lamp ?? await ReloadLampAsync().ConfigureAwait(false) ?? throw new FeatureNotSupportedException();
            current.BrightnessLevel = 1.0f;
            current.IsEnabled = true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task TurnOffAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (lamp is not null)
            {
                lamp.IsEnabled = false;
                lamp.Dispose();
                lamp = null;
                hasLoadedLamp = false;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
#endif
