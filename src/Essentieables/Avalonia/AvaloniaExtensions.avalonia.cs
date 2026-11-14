

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
//using AvaloniaFileSystem = Avalonia.Controls.Maui.Essentials.AvaloniaFileSystem;
//using AvaloniaHapticFeedback = Avalonia.Controls.Maui.Essentials.AvaloniaHapticFeedback;
//using AvaloniaPreferences = Avalonia.Controls.Maui.Essentials.AvaloniaPreferences;
//using AvaloniaScreenshot = Avalonia.Controls.Maui.Essentials.AvaloniaScreenshot;
//using AvaloniaMediaPicker = Avalonia.Controls.Maui.Essentials.AvaloniaMediaPicker;
using Dispatcher = Avalonia.Threading.Dispatcher;
using Avalonia;
using Avalonia.Controls.Maui.Essentials;

namespace Essentieables;

/// <summary>
/// Provides extension methods for registering Essentieables services
/// in an Avalonia application.
/// </summary>
internal static class AvaloniaExtensions
{
    /// <summary>
    /// Initializes Essentieables for an Avalonia application builder.
    /// </summary>
    /// <remarks>
    /// Avalonia has no service container of its own: this overload installs the implementations behind the static MAUI
    /// facades (<c>Preferences.Default</c>…) and registers them in a collection of its own, which nothing reads afterwards.
    /// When the app has a container, call <see cref="UseEssentieables(IServiceCollection)"/> on its collection instead, so
    /// <c>IPreferences</c> & co can be injected.
    /// </remarks>
    /// <param name="builder">The Avalonia application builder.</param>
    /// <returns>The original builder, allowing method chaining.</returns>
    public static AppBuilder UseEssentieables(this AppBuilder builder)
    {
        var services = new ServiceCollection();
        services.UseEssentieables();
        return builder;
    }

    /// <summary>
    /// Registers Essentieables implementations in the specified service collection.
    /// </summary>
    /// <remarks>
    /// Registers platform-specific implementations of .NET MAUI Essentials APIs
    /// and Avae-specific replacements. The selected implementations depend on
    /// the current operating system and its supported capabilities.
    ///
    /// Existing service registrations are preserved where the registration
    /// helpers use <see cref="ServiceCollectionDescriptorExtensions.TryAdd(
    /// IServiceCollection, ServiceDescriptor)"/> semantics.
    /// </remarks>
    /// <param name="services">
    /// The dependency injection service collection to configure.
    /// </param>
    public static void UseEssentieables(this IServiceCollection services)
    {
        // Create shared Avalonia-specific dependencies used by multiple services.
        // These instances are passed to the platform implementations below.
        var platformProvider = new AvaeTopLevelStateManager();
        var screenshot = new Avalonia.Controls.Maui.Essentials.AvaloniaScreenshot(platformProvider);
        var filepicker = new Avalonia.Controls.Maui.Essentials.AvaloniaFilePicker(platformProvider);
        var mediapicker = new Avalonia.Controls.Maui.Essentials.AvaloniaMediaPicker(platformProvider);
        var hapticFeedback = new Avalonia.Controls.Maui.Essentials.AvaloniaHapticFeedback();
        var preferences = new Avalonia.Controls.Maui.Essentials.AvaloniaPreferences();
        var fileSystem = new Avalonia.Controls.Maui.Essentials.AvaloniaFileSystem();
        var webAuthenticator = new Avalonia.Controls.Maui.Essentials.AvaloniaWebAuthenticator(platformProvider);

        // Each branch is compiled only for the target frameworks that can run it (ESSENTIEABLES_WINDOWS, ESSENTIEABLES_LINUX and
        // MACOS, set by build/Essentieables.targets and the SDK): an Android, iOS or browser app references neither the Windows
        // contracts nor the GTK4 Essentials.
#if ESSENTIEABLES_WINDOWS
        // Windows: use the Windows-specific .NET MAUI implementations where
        // supported by the current Windows version, with Avae replacements
        // for selected services.
        // .NET 10 runs on Windows 10 1607 and later, and the Windows App SDK projections need 1809 (17763): one guard for the
        // whole branch replaces a version check per service (the platform analyzer sees it).
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763, 0))
        {
            // The jump list: AppActionActivated is raised for the action the app was launched with (single instance is opt-in:
            // AvaeAppActionsActivation.TryForwardToRunningInstance() in Main).
            var appActions = new OriginalAppActionsImplementation();
            AvaeAppActionsActivation.Initialize(appActions);

            // The MAUI locations (%LOCALAPPDATA%\<Publisher>\<Package>, ApplicationData when packaged), so the files and preferences of
            // apps migrated from Avae.Windows.Essentials are found again. Package files missing on disk are read from avares://.
            IFileSystem windowsFileSystem = new OriginalFileSystemImplementation(fileSystem);
            IPreferences windowsPreferences = new OriginalPreferencesImplementation(windowsFileSystem);

            services.SetDefaults(
                new OriginalAccelerometerImplementation(),
                appActions,
                new AvaeAppInfo(platformProvider),
                new OriginalBarometerImplementation(),
                new OriginalBatteryImplementation(),
                new OriginalBrowserImplementation(),
                new OriginalClipboardImplementation(),
                new OriginalCompassImplementation(),
                new OriginalConnectivityImplementation(),
                new OriginalContactsImplementation(platformProvider),
                new AvaeDeviceDisplay(platformProvider),
                new OriginalDeviceInfoImplementation(),
                new Essentieables.AvaeEmail(),
                filepicker,
                windowsFileSystem,
                new OriginalFlashlightImplementation(),
#if WINDOWS
                Microsoft.Maui.Devices.Sensors.Geocoding.Default,
#else
                new AvaeGeocoding(),
#endif
                new AvaeGeolocation(),
                new OriginalGyroscopeImplementation(),
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063, 0) ? new OriginalHapticFeedbackImplementation() : hapticFeedback,
                new OriginalLauncherImplementation(),
                new OriginalMagnetometerImplementation(),
                new OriginalMapImplementation(),
                new AvaeMediaPicker(platformProvider, mediapicker),
                new OriginalOrientationSensorImplementation(),
                new AvaePhoneDialer(),
                windowsPreferences,
                screenshot,
                () => new OriginalSecureStorageImplementation(),
                new AvaeSemanticScreenReader(platformProvider),
                new OriginalShareImplementation(platformProvider),
                new OriginalSmsImplementation(),
                new OriginalTextToSpeechImplementation(),
                new OriginalVibrationImplementation(),
                // Loopback callbacks (http://localhost:port/) in the system browser, the others in the Avalonia broker.
                new AvaeWebAuthenticator(webAuthenticator),
                () => Microsoft.Maui.ApplicationModel.VersionTracking.Default);
        }
        else
#endif
#if ESSENTIEABLES_LINUX
        // Linux: register the Linux GTK4 implementations provided by .NET MAUI,
        // while using the shared Avalonia-specific services where applicable.
        if (OperatingSystem.IsLinux())
        {
            services.SetDefaults(
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxAccelerometer(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.AppModel.LinuxAppActions(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.AppModel.LinuxAppInfo(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxBarometer(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Devices.LinuxBattery(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.AppModel.LinuxBrowser(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.DataTransfer.LinuxClipboard(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxCompass(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Networking.LinuxConnectivity(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Communication.LinuxContacts(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Devices.LinuxDeviceDisplay(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Devices.LinuxDeviceInfo(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Communication.LinuxEmail(),
                filepicker,
                fileSystem,
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Devices.LinuxFlashlight(),
                new AvaeGeocoding(), // maui-labs LinuxGeocoding is a stub
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxGeolocation(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxGyroscope(),
                hapticFeedback,
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.AppModel.LinuxLauncher(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxMagnetometer(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.AppModel.LinuxMap(),
                mediapicker,
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Sensors.LinuxOrientationSensor(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Communication.LinuxPhoneDialer(),
                preferences,
                screenshot,
                () => OperatingSystem.IsLinux() ? new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Storage.LinuxSecureStorage() : null!,
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Accessibility.LinuxSemanticScreenReader(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.DataTransfer.LinuxShare(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Communication.LinuxSms(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Media.LinuxTextToSpeech(),
                new Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Devices.LinuxVibration(),
                new AvaeWebAuthenticator(webAuthenticator),
                () => VersionTracking.Default);
        }
        else
#endif
        // macOS: register the implementations supplied by the macOS-specific
        // integration. This branch is compiled only when MACOS is defined.
#if MACOS
        if (OperatingSystem.IsMacOS())
        {
            // NSApplication.CheckForIllegalCrossThreadCalls is left on: turning it off for the whole app would hide real
            // threading bugs. Call the Essentials APIs from the UI thread (AvaeMainThread), as AppKit requires.

            services.SetDefaults(
                null!,
                null!,
                (Microsoft.Maui.ApplicationModel.IAppInfo)MacosDefaults.CreateAppInfo(),
                null!,
                (Microsoft.Maui.Devices.IBattery)MacosDefaults.CreateBattery(),
                (Microsoft.Maui.ApplicationModel.IBrowser)MacosDefaults.CreateBrowser(),
                (Microsoft.Maui.ApplicationModel.DataTransfer.IClipboard)MacosDefaults.CreateClipboard(),
                null!,
                (Microsoft.Maui.Networking.IConnectivity)MacosDefaults.CreateConnectivity(),
                new AvaeMacContacts(),
                (Microsoft.Maui.Devices.IDeviceDisplay)MacosDefaults.CreateDeviceDisplay(),
                (Microsoft.Maui.Devices.IDeviceInfo)MacosDefaults.CreateDeviceInfo(),
                (Microsoft.Maui.ApplicationModel.Communication.IEmail)MacosDefaults.CreateEmail(),
                filepicker,
                fileSystem,
                (Microsoft.Maui.Devices.IFlashlight)MacosDefaults.CreateFlashlight(),
                new AvaeGeocoding(),
                (Microsoft.Maui.Devices.Sensors.IGeolocation)MacosDefaults.CreateGeolocation(),
                null!,
                (Microsoft.Maui.Devices.IHapticFeedback)MacosDefaults.CreateHapticFeedback(),
                (Microsoft.Maui.ApplicationModel.ILauncher)MacosDefaults.CreateLauncher(),
                null!,
                (Microsoft.Maui.ApplicationModel.IMap)MacosDefaults.CreateMap(),
                // Picking through Avalonia, capturing in a camera window (AVFoundation).
                new AvaeMacMediaPicker(mediapicker),
                null!,
                (Microsoft.Maui.ApplicationModel.Communication.IPhoneDialer)MacosDefaults.CreatePhoneDialer(),
                preferences,
                screenshot,
                () => (Microsoft.Maui.Storage.ISecureStorage)MacosDefaults.CreateSecureStorage(),
                (Microsoft.Maui.Accessibility.ISemanticScreenReader)MacosDefaults.CreateSemanticScreenReader(),
                (Microsoft.Maui.ApplicationModel.DataTransfer.IShare)MacosDefaults.CreateShare(),
                (Microsoft.Maui.ApplicationModel.Communication.ISms)MacosDefaults.CreateSms(),
                (Microsoft.Maui.Media.ITextToSpeech)MacosDefaults.CreateTextToSpeech(),
                (Microsoft.Maui.Devices.IVibration)MacosDefaults.CreateVibration(),
                // Loopback callbacks in the system browser, the others in ASWebAuthenticationSession.
                new AvaeWebAuthenticator(new AvaeMacWebAuthenticator()),
                () => Microsoft.Maui.ApplicationModel.VersionTracking.Default);
        }
        else
#endif
#if ESSENTIEABLES_AVALONIA_BROWSER
        // Browser: the implementations shared with Blazor (Blazor/*.cs and BlazorEssentials.js), run through
        // AvaloniaBrowserJSRuntime. Only the screenshot comes from Avalonia, which renders its own visual tree.
        // BlazorEssentials.InitializeAsync() is started here: await it before using the asynchronous APIs.
        if (OperatingSystem.IsBrowser())
        {
            BlazorEssentials.UseAvaloniaBrowser(services, screenshot);
        }
        else
#endif
        // Browser without the shared implementations (not compiled for this target): the Avalonia ones only.
        // Unsupported APIs are passed as null so SetDefaults can handle them.
        if (OperatingSystem.IsBrowser())
        {
            services.SetDefaults(
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                filepicker,
                fileSystem,
                null!,
                null!,
                null!,
                null!,
                hapticFeedback,
                null!,
                null!,
                null!,
                mediapicker,
                null!,
                null!,
                preferences,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                webAuthenticator,
                // No IAppInfo here: VersionTracking.Default would throw NotImplementedInReferenceAssemblyException.
                () => null!);
        }
        // Fallback: register the subset of services supported by the current
        // platform, leaving unsupported implementations unset.
        else
        {
            services.SetDefaults(
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                filepicker,
                fileSystem,
                null!,
                null!,
                null!,
                null!,
                hapticFeedback,
                null!,
                null!,
                null!,
                mediapicker,
                null!,
                null!,
                preferences,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                webAuthenticator,
                // No IAppInfo here: VersionTracking.Default would throw NotImplementedInReferenceAssemblyException.
                () => null!);
        }

        // Register Avae-specific services and any additional Essentials defaults.
        services.RegisterEssentieables();
    }
}