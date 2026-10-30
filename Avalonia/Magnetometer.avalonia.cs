// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Runtime.Versioning;
using Windows.Devices.Sensors;
using Dispatcher = Avalonia.Threading.Dispatcher;
using WindowsMagnetometer = Windows.Devices.Sensors.Magnetometer;

namespace Essentieables;

partial class OriginalMagnetometerImplementation : IMagnetometer
{
    bool UseSyncContext => SensorSpeed == SensorSpeed.Default || SensorSpeed == SensorSpeed.UI;

    public event EventHandler<MagnetometerChangedEventArgs>? ReadingChanged;

    public bool IsMonitoring { get; private set; }

    public bool IsSupported => PlatformIsSupported;

    SensorSpeed SensorSpeed { get; set; } = SensorSpeed.Default;

    public void Start(SensorSpeed sensorSpeed)
    {
        if (!PlatformIsSupported)
            throw new FeatureNotSupportedException();

        if (IsMonitoring)
            throw new InvalidOperationException("Magnetometer has already been started.");

        IsMonitoring = true;

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

    public void Stop()
    {
        if (!PlatformIsSupported)
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

    void RaiseReadingChanged(MagnetometerData data)
    {
        var args = new MagnetometerChangedEventArgs(data);

        if (UseSyncContext)
            Dispatcher.UIThread.Invoke(() => ReadingChanged?.Invoke(this, args));
        else
            ReadingChanged?.Invoke(this, args);
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalMagnetometerImplementation : IMagnetometer
{
    // keep around a reference so we can stop this same instance
    WindowsMagnetometer? sensor;

    static WindowsMagnetometer DefaultSensor =>
        WindowsMagnetometer.GetDefault();

    bool PlatformIsSupported =>
        DefaultSensor != null;

    void PlatformStart(SensorSpeed sensorSpeed)
    {
        sensor = DefaultSensor;

        var interval = sensorSpeed.ToPlatform();
        sensor.ReportInterval = sensor.MinimumReportInterval >= interval ? sensor.MinimumReportInterval : interval;

        sensor.ReadingChanged += DataUpdated;
    }

    void DataUpdated(object sender, MagnetometerReadingChangedEventArgs e)
    {
        var reading = e.Reading;
        var data = new MagnetometerData(reading.MagneticFieldX, reading.MagneticFieldY, reading.MagneticFieldZ);
        RaiseReadingChanged(data);
    }

    void PlatformStop()
    {
        if (sensor != null)
        {
            sensor.ReadingChanged -= DataUpdated;
            sensor.ReportInterval = 0;
            sensor = null;
        }
    }
}
