using Microsoft.Maui.Devices;
using System;
using System.Runtime.Versioning;
using Windows.System.Power;
using Dispatcher = Avalonia.Threading.Dispatcher;
using EnergySaverStatus = Microsoft.Maui.Devices.EnergySaverStatus;

namespace Essentieables;

partial class OriginalBatteryImplementation : IBattery
{
    event EventHandler<BatteryInfoChangedEventArgs>? BatteryInfoChangedInternal;

    event EventHandler<EnergySaverStatusChangedEventArgs>? EnergySaverStatusChangedInternal;

    public event EventHandler<BatteryInfoChangedEventArgs> BatteryInfoChanged
    {
        add
        {
            if (BatteryInfoChangedInternal == null)
                StartBatteryListeners();
            BatteryInfoChangedInternal += value;
        }
        remove
        {
            BatteryInfoChangedInternal -= value;
            if (BatteryInfoChangedInternal == null)
                StopBatteryListeners();
        }
    }

    public event EventHandler<EnergySaverStatusChangedEventArgs> EnergySaverStatusChanged
    {
        add
        {
            if (EnergySaverStatusChangedInternal == null)
                StartEnergySaverListeners();
            EnergySaverStatusChangedInternal += value;
        }
        remove
        {
            EnergySaverStatusChangedInternal -= value;
            if (EnergySaverStatusChangedInternal == null)
                StopEnergySaverListeners();
        }
    }

    // a cache so that events aren't fired unnecessarily
    // this is mainly an issue on Android, but we can stiil do this Essentials.Avalonia
    static double currentLevel;
    static BatteryPowerSource currentSource;
    static BatteryState currentState;

    void SetCurrent()
    {
        currentLevel = ChargeLevel;
        currentSource = PowerSource;
        currentState = State;
    }

    void OnBatteryInfoChanged(double level, BatteryState state, BatteryPowerSource source)
        => OnBatteryInfoChanged(new BatteryInfoChangedEventArgs(level, state, source));

    void OnBatteryInfoChanged()
        => OnBatteryInfoChanged(ChargeLevel, State, PowerSource);

    void OnBatteryInfoChanged(BatteryInfoChangedEventArgs e)
    {
        if (currentLevel != e.ChargeLevel || currentSource != e.PowerSource || currentState != e.State)
        {
            SetCurrent();
            BatteryInfoChangedInternal?.Invoke(null, e);
        }
    }

    void OnEnergySaverChanged()
        => OnEnergySaverChanged(EnergySaverStatus);

    void OnEnergySaverChanged(EnergySaverStatus saverStatus)
        => OnEnergySaverChanged(new EnergySaverStatusChangedEventArgs(saverStatus));

    void OnEnergySaverChanged(EnergySaverStatusChangedEventArgs e)
        => EnergySaverStatusChangedInternal?.Invoke(null, e);
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalBatteryImplementation : IBattery
{
    void StartEnergySaverListeners() =>
        PowerManager.EnergySaverStatusChanged += ReportEnergySaverUpdated;

    void StopEnergySaverListeners() =>
        PowerManager.EnergySaverStatusChanged -= ReportEnergySaverUpdated;

    void ReportEnergySaverUpdated(object? sender, object e)
        => Dispatcher.UIThread.Invoke(OnEnergySaverChanged);

    public void StartBatteryListeners() =>
        DefaultBattery.ReportUpdated += ReportUpdated;

    public void StopBatteryListeners() =>
        DefaultBattery.ReportUpdated -= ReportUpdated;

    void ReportUpdated(object sender, object e)
        => Dispatcher.UIThread.Invoke(OnBatteryInfoChanged);

    global::Windows.Devices.Power.Battery DefaultBattery =>
        global::Windows.Devices.Power.Battery.AggregateBattery;

    public double ChargeLevel
    {
        get
        {
            var finalReport = DefaultBattery.GetReport();
            var finalPercent = 1.0;

            var remaining = finalReport.RemainingCapacityInMilliwattHours;
            var full = finalReport.FullChargeCapacityInMilliwattHours;

            if (remaining.HasValue && full.HasValue)
                finalPercent = (double)remaining.Value / (double)full.Value;

            return finalPercent;
        }
    }

    public BatteryState State
    {
        get
        {
            var report = DefaultBattery.GetReport();

            switch (report.Status)
            {
                case BatteryStatus.Charging:
                    return BatteryState.Charging;
                case BatteryStatus.Discharging:
                    return BatteryState.Discharging;
                case BatteryStatus.Idle:
                    if (ChargeLevel >= 1.0)
                        return BatteryState.Full;
                    return BatteryState.NotCharging;
                case BatteryStatus.NotPresent:
                    return BatteryState.NotPresent;
            }

            if (ChargeLevel >= 1.0)
                return BatteryState.Full;

            return BatteryState.Unknown;
        }
    }

    public BatteryPowerSource PowerSource
    {
        get
        {
            switch (State)
            {
                case BatteryState.Full:
                case BatteryState.Charging:
                case BatteryState.NotPresent:
                    return BatteryPowerSource.AC;
                case BatteryState.Unknown:
                    return BatteryPowerSource.Unknown;
                default:
                    return BatteryPowerSource.Battery;
            }
        }
    }

    public EnergySaverStatus EnergySaverStatus =>
        PowerManager.EnergySaverStatus == global::Windows.System.Power.EnergySaverStatus.On ? EnergySaverStatus.On : EnergySaverStatus.Off;
}
