using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Runtime.Versioning;
using Windows.Devices.Sensors;
using Dispatcher = Avalonia.Threading.Dispatcher;
using WindowsGyro = Windows.Devices.Sensors.Gyrometer;

namespace Essentieables;

partial class OriginalGyroscopeImplementation : IGyroscope
{
    bool UseSyncContext => SensorSpeed == SensorSpeed.Default || SensorSpeed == SensorSpeed.UI;

    SensorSpeed SensorSpeed { get; set; } = SensorSpeed.Default;

    public event EventHandler<GyroscopeChangedEventArgs>? ReadingChanged;

    public bool IsMonitoring { get; private set; }

    public bool IsSupported => PlatformIsSupported;

    public void Start(SensorSpeed sensorSpeed)
    {
        if (!PlatformIsSupported)
            throw new FeatureNotSupportedException();

        if (IsMonitoring)
            throw new InvalidOperationException("Gyroscope has already been started.");

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

    void RaiseReadingChanged(GyroscopeData data)
    {
        var args = new GyroscopeChangedEventArgs(data);

        if (UseSyncContext)
            Dispatcher.UIThread.Invoke(() => ReadingChanged?.Invoke(null, args));
        else
            ReadingChanged?.Invoke(null, args);
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalGyroscopeImplementation : IGyroscope
{
    // keep around a reference so we can stop this same instance
    WindowsGyro? sensor;

    static WindowsGyro DefaultSensor =>
        WindowsGyro.GetDefault();

    bool PlatformIsSupported =>
        DefaultSensor != null;

    void PlatformStart(SensorSpeed sensorSpeed)
    {
        sensor = DefaultSensor;

        var interval = sensorSpeed.ToPlatform();
        sensor.ReportInterval = sensor.MinimumReportInterval >= interval ? sensor.MinimumReportInterval : interval;

        sensor.ReadingChanged += DataUpdated;
    }

    void DataUpdated(object sender, GyrometerReadingChangedEventArgs e)
    {
        var reading = e.Reading;
        var data = new GyroscopeData(reading.AngularVelocityX, reading.AngularVelocityY, reading.AngularVelocityZ);
        RaiseReadingChanged(data);
    }

    void PlatformStop()
    {
        if (sensor != null)
        {
            sensor.ReadingChanged -= DataUpdated;
            sensor.ReportInterval = 0;
        }
    }
}
