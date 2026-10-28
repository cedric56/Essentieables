#nullable enable
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using PositionChangedEventArgs = Windows.Devices.Geolocation.PositionChangedEventArgs;

namespace Essentieables
{
    [SupportedOSPlatform("windows10.0.14393")]
    public class AvaeGeolocation : IGeolocation
    {
        public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;

        public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;

        internal void OnLocationChanged(Location location) =>
            OnLocationChanged(new GeolocationLocationChangedEventArgs(location));

        internal void OnLocationChanged(GeolocationLocationChangedEventArgs e) =>
            LocationChanged?.Invoke(null, e);

        internal void OnLocationError(GeolocationError geolocationError) =>
            ListeningFailed?.Invoke(null, new GeolocationListeningFailedEventArgs(geolocationError));

        Geolocator? listeningGeolocator;

        /// <summary>
        /// Indicates if currently listening to location updates while the app is in foreground.
        /// </summary>
        public bool IsListeningForeground { get => listeningGeolocator != null; }

        public bool IsEnabled
        {
            get
            {
                var geolocator = new Geolocator();
                return geolocator.LocationStatus != PositionStatus.Disabled && geolocator.LocationStatus != PositionStatus.NotAvailable;
            }
        }

        public async Task<Location?> GetLastKnownLocationAsync()
        {
            // no need for permissions as AllowFallbackToConsentlessPositions
            // will allow the device to return a location regardless

            var geolocator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
            };
            geolocator.AllowFallbackToConsentlessPositions();

            var location = await geolocator.GetGeopositionAsync().AsTask();

            return location?.Coordinate?.ToLocation();
        }

        static void CheckStatus(PositionStatus status)
        {
            switch (status)
            {
                case PositionStatus.Disabled:
                case PositionStatus.NotAvailable:
                    throw new FeatureNotEnabledException("Location services are not enabled on device.");
            }
        }

        /// <summary>
        /// Stop listening for location updates when the app is in the foreground.
        /// Has no effect when not listening and <see cref="Geolocation.IsListeningForeground"/>
        /// is currently <see langword="false"/>.
        /// </summary>
        public void StopListeningForeground()
        {
            if (!IsListeningForeground || listeningGeolocator == null)
                return;

            listeningGeolocator.PositionChanged -= OnLocatorPositionChanged;
            listeningGeolocator.StatusChanged -= OnLocatorStatusChanged;

            listeningGeolocator = null;
        }

        void OnLocatorPositionChanged(Geolocator sender, PositionChangedEventArgs e) =>
            OnLocationChanged(e.Position.ToLocation());

        void OnLocatorStatusChanged(Geolocator sender, StatusChangedEventArgs e)
        {
            if (!IsListeningForeground)
                return;

            StopListeningForeground();

            GeolocationError error;
            switch (e.Status)
            {
                case PositionStatus.Disabled:
                    error = GeolocationError.Unauthorized;
                    break;

                case PositionStatus.NoData:
                    error = GeolocationError.PositionUnavailable;
                    break;

                default:
                    return;
            }

            OnLocationError(error);
        }

        public async Task<Location?> GetLocationAsync(Microsoft.Maui.Devices.Sensors.GeolocationRequest request, CancellationToken cancelToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            await AvaePermissions.EnsureGrantedAsync<AvaePermissions.LocationWhenInUse>();

            var geolocator = new Geolocator
            {
                DesiredAccuracyInMeters = request.ToGeolocationRequest().PlatformDesiredAccuracy
            };

            CheckStatus(geolocator.LocationStatus);

            cancelToken = Utils.TimeoutToken(cancelToken, request.Timeout);

            var location = await geolocator.GetGeopositionAsync().AsTask(cancelToken);

            return location?.Coordinate?.ToLocation();
        }

        public async Task<bool> StartListeningForegroundAsync(Microsoft.Maui.Devices.Sensors.GeolocationListeningRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.MinimumTime.TotalMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(request), "MinimumTime must be positive.");

            if (IsListeningForeground)
                throw new InvalidOperationException("Already listening to location updates.");

            await AvaePermissions.EnsureGrantedAsync<AvaePermissions.LocationWhenInUse>();

            listeningGeolocator = new Geolocator
            {
                DesiredAccuracyInMeters = request.ToGeolocationListeningRequest().PlatformDesiredAccuracy,
                ReportInterval = (uint)request.MinimumTime.TotalMilliseconds,
                MovementThreshold = request.ToGeolocationListeningRequest().PlatformDesiredAccuracy,
            };

            CheckStatus(listeningGeolocator.LocationStatus);

            listeningGeolocator.PositionChanged += OnLocatorPositionChanged;
            listeningGeolocator.StatusChanged += OnLocatorStatusChanged;

            return true;
        }
    }
}
