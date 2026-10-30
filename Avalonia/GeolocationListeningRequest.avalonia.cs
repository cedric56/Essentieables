// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

#nullable enable

using System;

namespace Essentieables
{
    internal static class GeolocationListeningExtensions
    {
        public static GeolocationListeningRequest ToGeolocationListeningRequest(this Microsoft.Maui.Devices.Sensors.GeolocationListeningRequest request)
        {
            return new GeolocationListeningRequest(
                request.DesiredAccuracy switch
                {
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Default => GeolocationAccuracy.Default,
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Best => GeolocationAccuracy.Best,
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.High => GeolocationAccuracy.High,
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Low => GeolocationAccuracy.Low,
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Lowest => GeolocationAccuracy.Lowest,
                    Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.Medium => GeolocationAccuracy.Medium,
                    _ => throw new InvalidOperationException()

                }, request.MinimumTime);
        }
    }

    internal partial class GeolocationListeningRequest
	{/// <summary>
     /// Creates a new request object with default values
     /// </summary>
        public GeolocationListeningRequest()
            : this(GeolocationAccuracy.Default)
        {
        }

        /// <summary>
        /// Creates a new request object with given accuracy.
        /// </summary>
        /// <param name="accuracy">The desired geolocation accuracy.</param>
        public GeolocationListeningRequest(GeolocationAccuracy accuracy)
            : this(accuracy, TimeSpan.FromSeconds(1))
        {
        }

        /// <summary>
        /// Creates a new request object with given accuracy and minimum time.
        /// </summary>
        /// <param name="accuracy">The desired geolocation accuracy.</param>
        /// <param name="minimumTime">The minimum time between location updates being sent.</param>
        public GeolocationListeningRequest(GeolocationAccuracy accuracy, TimeSpan minimumTime)
        {
            DesiredAccuracy = accuracy;
            MinimumTime = minimumTime;
        }

        /// <summary>
        /// Minimum time between location updates being sent. This value must positive. Most location
        /// sensors may not return locations in intervals shorter than 1 second.
        /// </summary>
        public TimeSpan MinimumTime { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The desired minimum accuracy for the location updates being sent. Locations that don't
        /// satisfy this accuracy are not sent using the event handler.
        /// </summary>
        public GeolocationAccuracy DesiredAccuracy { get; set; } = GeolocationAccuracy.Default;
        internal uint PlatformDesiredAccuracy
		{
			get
			{
				return DesiredAccuracy.PlatformGetDesiredAccuracy();
			}
		}
	}
}
