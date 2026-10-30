// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Numerics;
using System.Runtime.Versioning;
using Windows.Devices.Sensors;
using Dispatcher = Avalonia.Threading.Dispatcher;
using WindowsAccelerometer = Windows.Devices.Sensors.Accelerometer;

namespace Essentieables;

partial class OriginalAccelerometerImplementation : IAccelerometer
{
    const double accelerationThreshold = 169;

    const double gravity = 9.81;

    static readonly OriginalAccelerometerQueue queue = new OriginalAccelerometerQueue();

    static bool useSyncContext;

    /// <inheritdoc/>
    public event EventHandler<AccelerometerChangedEventArgs>? ReadingChanged;

    /// <inheritdoc/>
    public event EventHandler? ShakeDetected;

    /// <inheritdoc/>
    public bool IsMonitoring { get; private set; }

    /// <inheritdoc/>
    /// <exception cref="FeatureNotSupportedException">Thrown if <see cref="IsSupported"/> returns <see langword="false"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="IsMonitoring"/> returns <see langword="true"/>.</exception>
    public void Start(SensorSpeed sensorSpeed)
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();

        if (IsMonitoring)
            throw new InvalidOperationException("Accelerometer has already been started.");

        IsMonitoring = true;
        useSyncContext = sensorSpeed == SensorSpeed.Default || sensorSpeed == SensorSpeed.UI;

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

    internal void OnChanged(AccelerometerData reading) =>
        OnChanged(new AccelerometerChangedEventArgs(reading));

    internal void OnChanged(AccelerometerChangedEventArgs e)
    {
        if (useSyncContext)
            Dispatcher.UIThread.Invoke(() => ReadingChanged?.Invoke(null, e));
        else
            ReadingChanged?.Invoke(null, e);

        if (ShakeDetected != null)
            ProcessShakeEvent(e.Reading.Acceleration);
    }

    void ProcessShakeEvent(Vector3 acceleration)
    {
        var now = Nanoseconds(DateTime.UtcNow);

        var x = acceleration.X * gravity;
        var y = acceleration.Y * gravity;
        var z = acceleration.Z * gravity;

        var g = x * x + y * y + z * z;
        queue.Add(now, g > accelerationThreshold);

        if (queue.IsShaking)
        {
            queue.Clear();
            var args = new EventArgs();

            if (useSyncContext)
                Dispatcher.UIThread.Invoke(() => ShakeDetected?.Invoke(null, args));
            else
                ShakeDetected?.Invoke(null, args);
        }

        static long Nanoseconds(DateTime time) =>
            (time.Ticks / TimeSpan.TicksPerMillisecond) * 1_000_000;
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalAccelerometerImplementation
{
    // keep around a reference so we can stop this same instance
    WindowsAccelerometer? sensor;

    internal static WindowsAccelerometer DefaultSensor =>
        WindowsAccelerometer.GetDefault();

    public bool IsSupported =>
        DefaultSensor != null;

    void PlatformStart(SensorSpeed sensorSpeed)
    {
        sensor = DefaultSensor;

        var interval = sensorSpeed.ToPlatform();
        sensor.ReportInterval = sensor.MinimumReportInterval >= interval ? sensor.MinimumReportInterval : interval;

        sensor.ReadingChanged += DataUpdated;
    }

    void DataUpdated(object sender, AccelerometerReadingChangedEventArgs e)
    {
        var reading = e.Reading;
        var data = new AccelerometerData(reading.AccelerationX * -1, reading.AccelerationY * -1, reading.AccelerationZ * -1);
        OnChanged(data);
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
