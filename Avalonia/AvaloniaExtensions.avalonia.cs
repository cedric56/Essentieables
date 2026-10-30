using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;
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
public static class AvaloniaExtensions
{
    /// <summary>
    /// Initializes Essentieables for an Avalonia application builder.
    /// </summary>
    /// <remarks>
    /// This overload creates a new service collection and registers the
    /// services in it. The collection is not attached to the application's
    /// dependency injection container by this method.
    /// Use the <see cref="UseAvaeEssentials(IServiceCollection)"/> overload
    /// when registering services in the application's existing service collection.
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
        // Initialize the dispatcher used by Essentieables to marshal
        // operations onto the current Avalonia UI thread.
        AvaeDispatcher.Initialize(Dispatcher.CurrentDispatcher);

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

        // Windows: use the Windows-specific .NET MAUI implementations where
        // supported by the current Windows version, with Avae replacements
        // for selected services.
        if (OperatingSystem.IsWindows())
        {
            services.SetDefaults(
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalAccelerometerImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10586, 0) ? new OriginalAppActionsImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new AvaeAppInfo(platformProvider) : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalBarometerImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalBatteryImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalBrowserImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalClipboardImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalCompassImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalConnectivityImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalContactsImplementation(platformProvider) : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new AvaeDeviceDisplay(platformProvider) : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalDeviceInfoImplementation() : null!,
                new Essentieables.AvaeEmail(),
                filepicker,
                fileSystem,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalFlashlightImplementation() : null!,
#if WINDOWS
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763, 0) ? Microsoft.Maui.Devices.Sensors.Geocoding.Default : null!,
#else
                new AvaeGeocoding(),
#endif
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393, 0) ? new AvaeGeolocation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalGyroscopeImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063, 0) ? new OriginalHapticFeedbackImplementation() : hapticFeedback,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalLauncherImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalMagnetometerImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalMapImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new AvaeMediaPicker(platformProvider, mediapicker) : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalOrientationSensorImplementation() : null!,
                new AvaePhoneDialer(),
                preferences,
                screenshot,
                () => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalSecureStorageImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new AvaeSemanticScreenReader(platformProvider) : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalShareImplementation(platformProvider) : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalSmsImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalTextToSpeechImplementation() : null!,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240, 0) ? new OriginalVibrationImplementation() : null!,
                webAuthenticator,
                () => Microsoft.Maui.ApplicationModel.VersionTracking.Default);
        }
        // Linux: register the Linux GTK4 implementations provided by .NET MAUI,
        // while using the shared Avalonia-specific services where applicable.
        else if (OperatingSystem.IsLinux())
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
                webAuthenticator,
                () => VersionTracking.Default);
        }
        // macOS: register the implementations supplied by the macOS-specific
        // integration. This branch is compiled only when MACOS is defined.
#if MACOS
        else if (OperatingSystem.IsMacOS())
        {
            // Avalonia/MAUI interop may otherwise trigger cross-thread warnings
            // in APIs used by this integration.
            AppKit.NSApplication.CheckForIllegalCrossThreadCalls = false;

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
                null!,
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
                mediapicker,
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
                webAuthenticator,
                () => Microsoft.Maui.ApplicationModel.VersionTracking.Default);
        }
#endif
        // Browser: register only the services supported by this environment.
        // Unsupported APIs are passed as null so SetDefaults can handle them.
        else if (OperatingSystem.IsBrowser())
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

        // Expose the Avae dispatcher through MAUI's dispatcher abstraction.
        // TryAdd avoids replacing a dispatcher registered by the application.
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IDispatcher),
            _ => AvaeDispatcher.Default,
            ServiceLifetime.Singleton));

        // Register Avae-specific services and any additional Essentials defaults.
        services.RegisterAvaeEssentials();
    }
}