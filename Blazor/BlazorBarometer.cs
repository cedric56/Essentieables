// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;

namespace Essentieables;

/// <summary>No barometric pressure sensor is exposed to web content.</summary>
internal class BlazorBarometer : IBarometer
{
    public bool IsSupported => false;

    public bool IsMonitoring => false;

    public event EventHandler<BarometerChangedEventArgs>? ReadingChanged
    {
        add { }
        remove { }
    }

    public void Start(SensorSpeed sensorSpeed) =>
        throw new FeatureNotSupportedException("The barometer is not available in the browser.");

    public void Stop()
    {
    }
}
