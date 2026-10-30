// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System;
using System.Runtime.Versioning;
using Windows.Devices.Haptics;
using Windows.Foundation.Metadata;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalVibrationImplementation : IVibration
{
    public bool IsSupported
       => false; // Windows.Phone.Devices.Notification.VibrationDevice is not available to desktop apps.


    public void Vibrate()
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();

         throw new NotImplementedException("WINUI");// DefaultDevice.Vibrate(duration);
    }

    public void Vibrate(TimeSpan duration)
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();

        if (duration.TotalMilliseconds < 0)
            duration = TimeSpan.Zero;
        else if (duration.TotalSeconds > 5)
            duration = TimeSpan.FromSeconds(5);

        throw new NotImplementedException("WINUI");// DefaultDevice.Vibrate(duration);
    }

    public void Cancel()
    {
        if (!IsSupported)
            throw new FeatureNotSupportedException();

        throw new NotImplementedException("WINUI");// DefaultDevice.Vibrate(duration);
    }
}