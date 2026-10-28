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
       => ApiInformation.IsTypePresent("Windows.Phone.Devices.Notification.VibrationDevice") && DefaultDevice != null;

    static VibrationDevice DefaultDevice =>
        throw new NotImplementedException("WINUI"); //VibrationDevice.GetDefault();


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