// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Runtime.Versioning;
using Windows.Devices.Sensors;
using Dispatcher = Avalonia.Threading.Dispatcher;
using WindowsCompass = Windows.Devices.Sensors.Compass;

namespace Essentieables;

partial class OriginalCompassImplementation : ICompass
{
    bool UseSyncContext => SensorSpeed == SensorSpeed.Default || SensorSpeed == SensorSpeed.UI;

    public event EventHandler<CompassChangedEventArgs>? ReadingChanged;

    public bool IsSupported
        => PlatformIsSupported;

    public bool IsMonitoring { get; private set; }

    SensorSpeed SensorSpeed { get; set; }

    public void Start(SensorSpeed sensorSpeed) => Start(sensorSpeed, true);

    public void Start(SensorSpeed sensorSpeed, bool applyLowPassFilter)
    {
        if (!PlatformIsSupported)
            throw new FeatureNotSupportedException();

        if (IsMonitoring)
            throw new InvalidOperationException("Compass has already been started.");

        IsMonitoring = true;


        try
        {
            PlatformStart(sensorSpeed, applyLowPassFilter);
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

    internal void RaiseReadingChanged(CompassData data)
    {
        var args = new CompassChangedEventArgs(data);

        if (UseSyncContext)
            Dispatcher.UIThread.Invoke(() => ReadingChanged?.Invoke(null, args));
        else
            ReadingChanged?.Invoke(null, args);
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalCompassImplementation : ICompass
{
    // Magic numbers from https://docs.microsoft.com/en-us/uwp/api/windows.devices.sensors.compass.reportinterval#Windows_Devices_Sensors_Compass_ReportInterval
    internal const uint FastestInterval = 8;
    internal const uint GameInterval = 22;
    internal const uint NormalInterval = 33;

    // keep around a reference so we can stop this same instance
    WindowsCompass? sensor;

    static WindowsCompass DefaultCompass =>
        WindowsCompass.GetDefault();

    bool PlatformIsSupported =>
        DefaultCompass != null;

    void PlatformStart(SensorSpeed sensorSpeed, bool applyLowPassFilter)
    {
        sensor = DefaultCompass;

        var interval = NormalInterval;
        switch (sensorSpeed)
        {
            case SensorSpeed.Fastest:
                interval = FastestInterval;
                break;
            case SensorSpeed.Game:
                interval = GameInterval;
                break;
        }

        sensor.ReportInterval = sensor.MinimumReportInterval >= interval ? sensor.MinimumReportInterval : interval;

        sensor.ReadingChanged += CompassReportedInterval;
    }

    void CompassReportedInterval(object sender, CompassReadingChangedEventArgs e)
    {
        var data = new CompassData(e.Reading.HeadingMagneticNorth);
        RaiseReadingChanged(data);
    }

    void PlatformStop()
    {
        if (sensor != null)
        {
            sensor.ReadingChanged -= CompassReportedInterval;
            sensor.ReportInterval = 0;
        }
    }
}
