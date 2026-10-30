using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Windows.ApplicationModel;
using System.Collections.Concurrent;
using System.IO;
using Windows.ApplicationModel.Contacts;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using static Microsoft.Maui.ApplicationModel.Permissions;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace Essentieables
{
	[SupportedOSPlatform("windows10.0.10240")]
	internal static partial class AvaePermissions
	{
        private static readonly Lazy<HashSet<string>> DeclaredCapabilities =
    new(LoadDeclaredCapabilities);

        private static readonly ConcurrentDictionary<string, bool>
            CapabilityLookupCache = new(StringComparer.OrdinalIgnoreCase);

        static readonly Lazy<bool> _isPackagedAppLazy = new Lazy<bool>(() =>
		{
			try
			{
				if (Package.Current != null)
					return true;
			}
			catch
			{
				// no-op
			}

			return false;
		});

		/// <summary>
		/// Gets if this app is a packaged app.
		/// </summary>
		public static bool IsPackagedApp => _isPackagedAppLazy.Value;

		static readonly Lazy<string> platformGetFullAppPackageFilePath = new Lazy<string>(() =>
		{
			return IsPackagedApp
				? Package.Current.InstalledLocation.Path
				: AppContext.BaseDirectory;
		});

		/// <summary>
		/// Gets full application path.
		/// </summary>
		public static string PlatformGetFullAppPackageFilePath => platformGetFullAppPackageFilePath.Value;


        /// <summary>
        /// Checks if the capability specified in <paramref name="capabilityName"/> is declared in the application's <c>AppxManifest.xml</c> file.
        /// </summary>
        /// <param name="capabilityName">The capability to check for specification in the <c>AppxManifest.xml</c> file.</param>
        /// <returns><see langword="true"/> when the capability is specified, otherwise <see langword="false"/>.</returns>
        public static bool IsCapabilityDeclared(string capabilityName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);

            // Unpackaged Win32 apps do not have an installed Appx manifest.
            // Manifest declarations are therefore not applicable to them.
            if (!IsPackagedApp)
                return true;

            return CapabilityLookupCache.GetOrAdd(
                capabilityName,
                name => DeclaredCapabilities.Value.Contains(name));
        }

        private static HashSet<string> LoadDeclaredCapabilities()
        {
            var capabilities = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var manifestPath = OriginalFileSystemUtils
                .PlatformGetFullAppPackageFilePath(
                    PlatformUtils.AppManifestFilename);

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException(
                    "The packaged application manifest could not be found.",
                    manifestPath);

            var doc = XDocument.Load(manifestPath, LoadOptions.None);
            if (doc.Root is null)
                return capabilities;

            XNamespace foundation = PlatformUtils.AppManifestXmlns;
            XNamespace uap = PlatformUtils.AppManifestUapXmlns;

            foreach (var element in doc.Root.Descendants())
            {
                if (element.Name == foundation + "Capability" ||
                    element.Name == foundation + "DeviceCapability" ||
                    element.Name == uap + "Capability")
                {
                    var name = (string?)element.Attribute("Name");
                    if (!string.IsNullOrWhiteSpace(name))
                        capabilities.Add(name);
                }
            }

            return capabilities;
        }

        internal static async Task EnsureGrantedAsync<TPermission>()
            where TPermission : AvaeBasePlatformPermission, new()
        {
            var status = await RequestAsync<TPermission>();

            if (status != PermissionStatus.Granted)
                throw new PermissionException($"{typeof(TPermission).Name} permission was not granted: {status}");
        }

        internal static async Task EnsureGrantedOrRestrictedAsync<TPermission>()
            where TPermission : AvaeBasePlatformPermission, new()
        {
            var status = await RequestAsync<TPermission>();

            if (status != PermissionStatus.Granted && status != PermissionStatus.Restricted)
                throw new PermissionException($"{typeof(TPermission).Name} permission was not granted or restricted: {status}");
        }


        /// <summary>
        /// Represents the platform-specific abstract base class for all permissions on this platform.
        /// </summary>
        public abstract partial class AvaeBasePlatformPermission : BasePermission
		{
			/// <summary>
			/// Gets the required entries that need to be present in the application's <c>AppxManifest.xml</c> file for this permission.
			/// </summary>
			protected virtual Func<IEnumerable<string>> RequiredDeclarations { get; } = () => Array.Empty<string>();

			/// <inheritdoc/>
			public override Task<PermissionStatus> CheckStatusAsync()
			{
				EnsureDeclared();
				return Task.FromResult(PermissionStatus.Granted);
			}

			/// <inheritdoc/>
			public override Task<PermissionStatus> RequestAsync()
				=> CheckStatusAsync();

			/// <inheritdoc/>
			public override void EnsureDeclared()
			{
				foreach (var d in RequiredDeclarations())
				{
					if (!IsCapabilityDeclared(d))
						throw new PermissionException($"You need to declare the capability `{d}` in your AppxManifest.xml file");
				}
			}

			/// <inheritdoc/>
			public override bool ShouldShowRationale() => false;
		}

		public partial class Battery : AvaeBasePlatformPermission
        {
		}

		public partial class Bluetooth : AvaeBasePlatformPermission
        {
		}

		public partial class CalendarRead : AvaeBasePlatformPermission
        {
		}

		public partial class CalendarWrite : AvaeBasePlatformPermission
        {
		}

		public partial class Camera : AvaeBasePlatformPermission
        {
		}

		public partial class ContactsRead : AvaeBasePlatformPermission
        {
			/// <inheritdoc/>
			public override async Task<PermissionStatus> CheckStatusAsync()
			{
				EnsureDeclared();
				var accessStatus = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);

				if (accessStatus == null)
					return PermissionStatus.Denied;

				return PermissionStatus.Granted;
			}
		}

		public partial class ContactsWrite : AvaeBasePlatformPermission
        {
			/// <inheritdoc/>
			public override async Task<PermissionStatus> CheckStatusAsync()
			{
				EnsureDeclared();
				var accessStatus = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);

				if (accessStatus == null)
					return PermissionStatus.Denied;

				return PermissionStatus.Granted;
			}
		}

		public partial class Flashlight : AvaeBasePlatformPermission
        {
		}

		public partial class LaunchApp : AvaeBasePlatformPermission
        {
		}

		public partial class LocationWhenInUse : AvaeBasePlatformPermission
        {
			/// <inheritdoc/>
			public override Task<PermissionStatus> CheckStatusAsync()
			{
				EnsureDeclared();
				return RequestLocationPermissionAsync();
			}

			internal static async Task<PermissionStatus> RequestLocationPermissionAsync()
			{
				if (!Dispatcher.UIThread.CheckAccess())
					throw new PermissionException("Permission request must be invoked on main thread.");

				var accessStatus = await Geolocator.RequestAccessAsync();
				return accessStatus switch
				{
					GeolocationAccessStatus.Allowed => PermissionStatus.Granted,
					GeolocationAccessStatus.Unspecified => PermissionStatus.Unknown,
					_ => PermissionStatus.Denied,
				};
			}
		}

		public partial class LocationAlways : AvaeBasePlatformPermission
        {
			/// <inheritdoc/>
			public override Task<PermissionStatus> CheckStatusAsync()
			{
				EnsureDeclared();
				return LocationWhenInUse.RequestLocationPermissionAsync();
			}
		}

		public partial class Maps : AvaeBasePlatformPermission
        {
		}

		public partial class Media : AvaeBasePlatformPermission
        {
		}

		public partial class Microphone : AvaeBasePlatformPermission
        {
		}

		public partial class NearbyWifiDevices : AvaeBasePlatformPermission
        {
		}

		public partial class NetworkState : AvaeBasePlatformPermission
        {
		}

		public partial class Phone : AvaeBasePlatformPermission
        {
		}

		public partial class Photos : AvaeBasePlatformPermission
        {
		}

		public partial class PostNotifications : AvaeBasePlatformPermission
        {
		}

		public partial class Reminders : AvaeBasePlatformPermission
        {
		}

		public partial class Sensors : AvaeBasePlatformPermission
        {
			static readonly Guid activitySensorClassId = new Guid("9D9E0118-1807-4F2E-96E4-2CE57142E196");

			/// <inheritdoc/>
			public override Task<PermissionStatus> CheckStatusAsync()
			{
				// Determine if the user has allowed access to activity sensors
				var deviceAccessInfo = DeviceAccessInformation.CreateFromDeviceClassId(activitySensorClassId);
				switch (deviceAccessInfo.CurrentStatus)
				{
					case DeviceAccessStatus.Allowed:
						return Task.FromResult(PermissionStatus.Granted);
					case DeviceAccessStatus.DeniedBySystem:
					case DeviceAccessStatus.DeniedByUser:
						return Task.FromResult(PermissionStatus.Denied);
					default:
						return Task.FromResult(PermissionStatus.Unknown);
				}
			}
		}

		public partial class Sms : AvaeBasePlatformPermission
        {
		}

		public partial class Speech : AvaeBasePlatformPermission
        {
		}

		public partial class StorageRead : AvaeBasePlatformPermission
        {
		}

		public partial class StorageWrite : AvaeBasePlatformPermission
        {
		}

		public partial class Vibrate : AvaeBasePlatformPermission
        {
		}
	}
}
