// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

#nullable enable

using Microsoft.Maui.Devices.Sensors;

namespace Essentieables;

static class GeolocationAccuracyExtensionMethods
{
    internal static uint PlatformGetDesiredAccuracy(this GeolocationAccuracy desiredAccuracy)
    {
        switch (desiredAccuracy)
        {
            case GeolocationAccuracy.Lowest:
                return 3000;
            case GeolocationAccuracy.Low:
                return 1000;
            case GeolocationAccuracy.Default:
            case GeolocationAccuracy.Medium:
                return 100;
            case GeolocationAccuracy.High:
                return 10; // Equivalent to PositionAccuracy.High
            case GeolocationAccuracy.Best:
                return 1;
            default:
                return 500; // Equivalent to PositionAccuracy.Default
        }
    }
}
