using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Runtime.Versioning;
using Windows.Devices.Sensors;
using Dispatcher = Avalonia.Threading.Dispatcher;
using WindowsOrientationSensor = Windows.Devices.Sensors.OrientationSensor;

namespace Essentieables;

/// <summary>
/// Concrete implementation of the <see cref="Microsoft.Maui.Devices.Sensors.IOrientationSensor"/> APIs.
/// </summary>
internal partial class OriginalOrientationSensorImplementation : IOrientationSensor
{
    bool UseSyncContext => SensorSpeed == SensorSpeed.Default || SensorSpeed == SensorSpeed.UI;

    SensorSpeed SensorSpeed { get; set; } = SensorSpeed.Default;

    /// <summary>
    /// Occurs when the orientation reading changes.
    /// </summary>
    public event EventHandler<OrientationSensorChangedEventArgs>? ReadingChanged;

    /// <summary>
    /// Gets a value indicating whether reading the orientation sensor is supported on this device.
    /// </summary>
    public bool IsSupported
        => PlatformIsSupported;

    /// <summary>
    /// Gets a value indicating whether the orientation sensor is actively being monitored.
    /// </summary>
    public bool IsMonitoring { get; private set; }

    /// <summary>
    /// Start monitoring for changes to the orientation.
    /// </summary>
    /// <remarks>
    /// Will throw <see cref="FeatureNotSupportedException"/> if not supported on device.
    /// Will throw <see cref="InvalidOperationException"/> if <see cref="IsMonitoring"/> is <see langword="true"/>.
    /// </remarks>
    /// <param name="sensorSpeed">The speed to listen for changes.</param>
    public void Start(SensorSpeed sensorSpeed)
    {
        if (!PlatformIsSupported)
            throw new FeatureNotSupportedException();

        if (IsMonitoring)
            throw new InvalidOperationException("Orientation sensor has already been started.");

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

    /// <summary>
    /// Stop monitoring for changes to the orientation.
    /// </summary>
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

    internal void RaiseReadingChanged(OrientationSensorData reading)
    {
        var args = new OrientationSensorChangedEventArgs(reading);

        if (UseSyncContext)
            Dispatcher.UIThread.Invoke(() => ReadingChanged?.Invoke(null, args));
        else
            ReadingChanged?.Invoke(null, args);
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalOrientationSensorImplementation : IOrientationSensor
{
    // keep around a reference so we can stop this same instance
    WindowsOrientationSensor? sensor;

    static WindowsOrientationSensor DefaultSensor =>
        WindowsOrientationSensor.GetDefault();

    bool PlatformIsSupported =>
        DefaultSensor != null;

    void PlatformStart(SensorSpeed sensorSpeed)
    {
        sensor = DefaultSensor;

        var interval = sensorSpeed.ToPlatform();

        sensor.ReportInterval = sensor.MinimumReportInterval >= interval ? sensor.MinimumReportInterval : interval;
        sensor.ReadingChanged += DataUpdated;
    }

    void DataUpdated(object sender, OrientationSensorReadingChangedEventArgs e)
    {
        var reading = e.Reading;
        var data = new OrientationSensorData(reading.Quaternion.X, reading.Quaternion.Y, reading.Quaternion.Z, reading.Quaternion.W);
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
