using Microsoft.JSInterop;
using Microsoft.Maui.Devices;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Battery info backed by navigator.getBattery() (Battery Status API — Chromium only).
/// Where the API is unavailable the state reads as Unknown with a full charge level.
/// </summary>
public sealed class BlazorBattery : IBattery, IAsyncDisposable
{
    public sealed record Snapshot(double Level, bool Charging);

    private double _chargeLevel = 1.0;
    private BatteryState _state = BatteryState.Unknown;
    private BatteryPowerSource _powerSource = BatteryPowerSource.Unknown;

    DotNetObjectReference<BlazorBattery>? _ref;

    /// <summary>Imports the module, loads the initial snapshot, and subscribes to changes. Idempotent.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var snapshot = await BlazorEssentials.Module.InvokeAsync<Snapshot?>("batGetSnapshot");
            if (snapshot is not null)
                Apply(snapshot);
            _ref?.Dispose();
            _ref = DotNetObjectReference.Create(this);
            await BlazorEssentials.Module.InvokeVoidAsync("batSubscribe", _ref);
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

    [JSInvokable]
    public void OnBatteryChanged(Snapshot snapshot)
    {
        Apply(snapshot);
        BatteryInfoChanged?.Invoke(this, new BatteryInfoChangedEventArgs(_chargeLevel, _state, _powerSource));
    }

    private void Apply(Snapshot s)
    {
        _chargeLevel = s.Level;
        _state = s.Charging
            ? (s.Level >= 1.0 ? BatteryState.Full : BatteryState.Charging)
            : BatteryState.Discharging;
        _powerSource = s.Charging ? BatteryPowerSource.AC : BatteryPowerSource.Battery;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ref is not null)
        {
            try
            { 
                await BlazorEssentials.Module.InvokeVoidAsync(
                "batUnsubscribe",
                _ref);
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
            _ref.Dispose();
            _ref = null;
        }
    }

    // ───────────────────────── IBattery (sync, cached) ─────────────────────────

    public double ChargeLevel => _chargeLevel;
    public BatteryState State => _state;
    public BatteryPowerSource PowerSource => _powerSource;
    public EnergySaverStatus EnergySaverStatus => EnergySaverStatus.Unknown;

    public event EventHandler<BatteryInfoChangedEventArgs>? BatteryInfoChanged;

    public event EventHandler<EnergySaverStatusChangedEventArgs>? EnergySaverStatusChanged
    {
        add { } // Browsers expose no energy-saver signal; the event never fires.
        remove { }
    }
}