using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Extension methods for registering .NET MAUI Essentials services with dependency injection,
/// looking up locale-to-country mappings, and bridging Avae-specific file/email/share behavior
/// with the underlying MAUI Essentials APIs.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Assigns the supplied MAUI Essentials implementations as the current defaults (via
    /// <see cref="EssentialsAccessors"/>), then registers them with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="accelerometer">The accelerometer implementation to use as the default.</param>
    /// <param name="appActions">The app actions implementation to use as the default.</param>
    /// <param name="appInfo">The app info implementation to use as the default.</param>
    /// <param name="barometer">The barometer implementation to use as the default.</param>
    /// <param name="battery">The battery implementation to use as the default.</param>
    /// <param name="browser">The browser implementation to use as the default.</param>
    /// <param name="clipboard">The clipboard implementation to use as the default.</param>
    /// <param name="compass">The compass implementation to use as the default.</param>
    /// <param name="connectivity">The connectivity implementation to use as the default.</param>
    /// <param name="contacts">The contacts implementation to use as the default.</param>
    /// <param name="deviceDisplay">The device display implementation to use as the default.</param>
    /// <param name="deviceInfo">The device info implementation to use as the default.</param>
    /// <param name="email">The email implementation to use as the default.</param>
    /// <param name="filepicker">The file picker implementation to use as the default.</param>
    /// <param name="fileSystem">The file system implementation to use as the default.</param>
    /// <param name="flashlight">The flashlight implementation to use as the default.</param>
    /// <param name="geocoding">The geocoding implementation to use as the default.</param>
    /// <param name="geolocation">The geolocation implementation to use as the default.</param>
    /// <param name="gyroscope">The gyroscope implementation to use as the default.</param>
    /// <param name="hapticFeedback">The haptic feedback implementation to use as the default.</param>
    /// <param name="launcher">The launcher implementation to use as the default.</param>
    /// <param name="magnetometer">The magnetometer implementation to use as the default.</param>
    /// <param name="map">The map implementation to use as the default.</param>
    /// <param name="mediaPicker">The media picker implementation to use as the default.</param>
    /// <param name="orientationSensor">The orientation sensor implementation to use as the default.</param>
    /// <param name="phoneDialer">The phone dialer implementation to use as the default.</param>
    /// <param name="preferences">The preferences implementation to use as the default.</param>
    /// <param name="screenshot">The screenshot implementation to use as the default.</param>
    /// <param name="secureStorage">A factory returning the secure storage implementation to use as the default.</param>
    /// <param name="semanticScreenReader">The semantic screen reader implementation to use as the default.</param>
    /// <param name="share">The share implementation to use as the default.</param>
    /// <param name="sms">The SMS implementation to use as the default.</param>
    /// <param name="textToSpeech">The text-to-speech implementation to use as the default.</param>
    /// <param name="vibration">The vibration implementation to use as the default.</param>
    /// <param name="webAuthenticator">The web authenticator implementation to use as the default.</param>
    /// <param name="versionTracking">A factory returning the version tracking implementation to use as the default.</param>
    /// <param name="lifetime">The service lifetime used when registering these implementations with the container. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    public static void SetDefaults(this IServiceCollection services,
        IAccelerometer accelerometer,
        IAppActions appActions,
        IAppInfo appInfo,
        IBarometer barometer,
        IBattery battery,
        IBrowser browser,
        IClipboard clipboard,
        ICompass compass,
        IConnectivity connectivity,
        IContacts contacts,
        IDeviceDisplay deviceDisplay,
        IDeviceInfo deviceInfo,
        IEmail email,
        IFilePicker filepicker,
        IFileSystem fileSystem,
        IFlashlight flashlight,
        IGeocoding geocoding,
        IGeolocation geolocation,
        IGyroscope gyroscope,
        IHapticFeedback hapticFeedback,
        ILauncher launcher,
        IMagnetometer magnetometer,
        IMap map,
        IMediaPicker mediaPicker,
        IOrientationSensor orientationSensor,
        IPhoneDialer phoneDialer,
        IPreferences preferences,
        IScreenshot screenshot,
        Func<ISecureStorage> secureStorage,
        ISemanticScreenReader semanticScreenReader,
        IShare share,
        ISms sms,
        ITextToSpeech textToSpeech,
        IVibration vibration,
        IWebAuthenticator webAuthenticator,
        Func<IVersionTracking> versionTracking,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        EssentialsAccessors.SetScreenshot(null, screenshot);
        EssentialsAccessors.SetFilePicker(null, filepicker);
        EssentialsAccessors.SetMediaPicker(null, mediaPicker);
        EssentialsAccessors.SetHapticFeedback(null, hapticFeedback);
        EssentialsAccessors.SetPreferences(null, preferences);
        EssentialsAccessors.SetFileSystem(null, fileSystem);
        EssentialsAccessors.SetWebAuthenticator(null, webAuthenticator);
        EssentialsAccessors.SetAccelerometer(null, accelerometer);
        EssentialsAccessors.SetAppActions(null, appActions);
        EssentialsAccessors.SetAppInfo(null, appInfo);
        EssentialsAccessors.SetBarometer(null, barometer);
        EssentialsAccessors.SetBattery(null, battery);
        EssentialsAccessors.SetBrowser(null, browser);
        EssentialsAccessors.SetClipboard(null, clipboard);
        EssentialsAccessors.SetCompass(null, compass);
        EssentialsAccessors.SetConnectivity(null, connectivity);
        EssentialsAccessors.SetContacts(null, contacts);
        EssentialsAccessors.SetDeviceDisplay(null, deviceDisplay);
        EssentialsAccessors.SetDeviceInfo(null, deviceInfo);
        EssentialsAccessors.SetEmail(null, email);
        EssentialsAccessors.SetFlashlight(null, flashlight);
        EssentialsAccessors.SetGeocoding(null, geocoding);
        EssentialsAccessors.SetGeolocation(null, geolocation);
        EssentialsAccessors.SetGyroscope(null, gyroscope);
        EssentialsAccessors.SetLauncher(null, launcher);
        EssentialsAccessors.SetMagnetometer(null, magnetometer);
        EssentialsAccessors.SetMap(null, map);
        EssentialsAccessors.SetOrientationSensor(null, orientationSensor);
        EssentialsAccessors.SetPhoneDialer(null, phoneDialer);
        EssentialsAccessors.SetSecureStorage(null, secureStorage?.Invoke());
        EssentialsAccessors.SetSemanticScreenReader(null, semanticScreenReader);
        EssentialsAccessors.SetShare(null, share);
        EssentialsAccessors.SetSms(null, sms);
        EssentialsAccessors.SetTextToSpeech(null, textToSpeech);
        EssentialsAccessors.SetVibration(null, vibration);
        EssentialsAccessors.SetVersionTracking(null, versionTracking?.Invoke());
        services.RegisterAvaeEssentials(lifetime);
    }

    /// <summary>
    /// Registers the platform-default implementations of all MAUI Essentials service interfaces
    /// with the dependency injection container, without overwriting any already-registered implementation.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="lifetime">The service lifetime used when registering these implementations. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    public static void RegisterAvaeEssentials(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        services.TryAdd(ServiceDescriptor.Describe(typeof(IAccelerometer), _ => Accelerometer.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IAppActions), _ => AppActions.Current, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IAppInfo), _ => AppInfo.Current, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IBarometer), _ => Barometer.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IBattery), _ => Battery.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IBrowser), _ => Browser.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IClipboard), _ => Clipboard.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ICompass), _ => Compass.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IConnectivity), _ => Connectivity.Current, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IContacts), _ => Microsoft.Maui.ApplicationModel.Communication.Contacts.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IDeviceDisplay), _ => DeviceDisplay.Current, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IDeviceInfo), _ => DeviceInfo.Current, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IEmail), _ => Email.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IFilePicker), _ => FilePicker.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IFileSystem), _ => FileSystem.Current, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IFlashlight), _ => Flashlight.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IGeocoding), _ => Geocoding.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IGeolocation), _ => Geolocation.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IGyroscope), _ => Gyroscope.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IHapticFeedback), _ => HapticFeedback.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ILauncher), _ => Launcher.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IMagnetometer), _ => Magnetometer.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IMap), _ => Map.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IMediaPicker), _ => MediaPicker.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IOrientationSensor), _ => OrientationSensor.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IPhoneDialer), _ => PhoneDialer.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IPreferences), _ => Preferences.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IScreenshot), _ => Screenshot.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ISecureStorage), _ => SecureStorage.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ISemanticScreenReader), _ => SemanticScreenReader.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IShare), _ => Share.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ISms), _ => Sms.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ITextToSpeech), _ => TextToSpeech.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IVibration), _ => Vibration.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IVersionTracking), _ =>VersionTracking.Default, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IWebAuthenticator), _ => WebAuthenticator.Default, lifetime));
    }

    /// <summary>
    /// Opens a readable stream for the specified file, using the Avae-specific implementation
    /// when available, or falling back to the standard MAUI Essentials behavior.
    /// </summary>
    /// <param name="file">The file to open.</param>
    /// <param name="overridesMauiPlatform">
    /// Reserved for future use; does not currently affect behavior beyond selecting the Avae-specific path when available.
    /// </param>
    /// <returns>A task that resolves to a readable stream for the file's contents.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="file"/> is <see langword="null"/>.</exception>
    public static Task<Stream> OpenReadAsync(
    this FileBase file,
    bool overridesMauiPlatform = true)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (overridesMauiPlatform && file is IAvaeFileResult avaeFileResult)
            return avaeFileResult.OpenFileStreamAsync();

        return file.OpenReadAsync();
    }

    /// <summary>
    /// Composes an email with the specified files attached, using the Avae-specific implementation
    /// when available, or converting the files to standard <see cref="EmailAttachment"/>s otherwise.
    /// </summary>
    /// <param name="email">The email service to compose with.</param>
    /// <param name="files">The files to attach to the email.</param>
    /// <param name="message">The email message to compose.</param>
    /// <returns>A task representing the asynchronous compose operation.</returns>
    public static Task ComposeAsync(
    this IEmail email,
    IEnumerable<FileBase> files,
    EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(message);

        var incomingFiles = files?.ToList() ?? [];

        if (email is IAvaeEmail avae)
            return avae.ComposeAsync(incomingFiles, message);

        var attachments = message.Attachments?.ToList() ?? [];

        foreach (var file in incomingFiles)
        {
            attachments.Add(new EmailAttachment(file.FullPath));
        }

        message.Attachments = attachments;
        return email.ComposeAsync(message);
    }

    /// <summary>
    /// Requests a native share of the specified files, using the Avae-specific implementation
    /// when available, or converting the files to a standard <see cref="ShareMultipleFilesRequest"/> otherwise.
    /// </summary>
    /// <param name="share">The share service to request with.</param>
    /// <param name="title">The title shown in the share dialog.</param>
    /// <param name="files">The files to share.</param>
    /// <returns>A task representing the asynchronous share operation.</returns>
    public static Task RequestAsync(this IShare share, string title, IEnumerable<FileBase> files)
    {
        if (share is IAvaeShare avae)
        {
            return avae.RequestAsync(title, files);
        }
        else
        {
            // Execute the native share request with the converted files
            return share.RequestAsync(new ShareMultipleFilesRequest()
            {
                Title = title,
                Files = [.. (files ?? []).Select(f => new ShareFile(f))]
            });
        }
    }
}