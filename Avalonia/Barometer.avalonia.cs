// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Runtime.Versioning;
using Windows.Devices.Sensors;
using Dispatcher = Avalonia.Threading.Dispatcher;
using WinBarometer = Windows.Devices.Sensors.Barometer;

namespace Essentieables;

partial class OriginalBarometerImplementation : IBarometer
{
    bool UseSyncContext => SensorSpeed == SensorSpeed.Default || SensorSpeed == SensorSpeed.UI;

#pragma warning disable CS0067
    public event EventHandler<BarometerChangedEventArgs>? ReadingChanged;
#pragma warning restore CS0067

    public bool IsMonitoring { get; private set; }

    SensorSpeed SensorSpeed { get; set; } = SensorSpeed.Default;

    void RaiseReadingChanged(BarometerData reading)
    {
        var args = new BarometerChangedEventArgs(reading);

        if (UseSyncContext)
            Dispatcher.UIThread.Invoke(() => ReadingChanged?.Invoke(this, args));
        else
            ReadingChanged?.Invoke(this, args);
    }

    /// <inheritdoc/>
    /// <exception cref="FeatureNotSupportedException">Thrown if <see cref="IsSupported"/> returns <see langword="false"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="IsMonitoring"/> returns <see langword="true"/>.</exception>
    public void Start(SensorSpeed sensorSpeed)
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();

        if (IsMonitoring)
            throw new InvalidOperationException("Barometer has already been started.");

        IsMonitoring = true;
        SensorSpeed = sensorSpeed;

        try
        {
            PlatformStart(sensorSpeed);
        }
        catch
        {
            IsMonitoring = false;
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="FeatureNotSupportedException">Thrown if <see cref="IsSupported"/> returns <see langword="false"/>.</exception>
    public void Stop()
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();

        if (!IsMonitoring)
            return;

        IsMonitoring = false;

        try
        {
            PlatformStop();
        }
        catch
        {
            IsMonitoring = true;
            throw;
        }
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalBarometerImplementation : IBarometer
{
    WinBarometer? sensor;

    WinBarometer DefaultBarometer => WinBarometer.GetDefault();

    public bool IsSupported =>
        DefaultBarometer != null;

    void PlatformStart(SensorSpeed sensorSpeed)
    {
        sensor = DefaultBarometer;

        var interval = sensorSpeed.ToPlatform();
        sensor.ReportInterval = sensor.MinimumReportInterval >= interval ? sensor.MinimumReportInterval : interval;

        sensor.ReadingChanged += BarometerReportedInterval;
    }

    internal void BarometerReportedInterval(object sender, BarometerReadingChangedEventArgs e)
        => RaiseReadingChanged(new BarometerData(e.Reading.StationPressureInHectopascals));

    void PlatformStop()
    {
        if (sensor == null)
            return;

        sensor.ReadingChanged -= BarometerReportedInterval;
        sensor.ReportInterval = 0;
        sensor = null;
    }
}
