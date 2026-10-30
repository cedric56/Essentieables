// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;

namespace Essentieables;

internal static class BlazorEssentials
{
    public static void UseBlazorEssentieables(this IServiceCollection services)
    {
        var appInfo = new BlazorAppInfo();
        var preferences = new BlazorPreferences();
        var browser = new BlazorBrowser();
        services.SetDefaults(
            new BlazorAccelerometer(),
            new BlazorAppActions(),
            appInfo,
            new BlazorBarometer(),
            new BlazorBattery(),
            browser,
            new BlazorClipboard(),
            new BlazorCompass(),
            new BlazorConnectivity(),
            new BlazorContacts(),
            new BlazorDeviceDisplay(),
            new BlazorDeviceInfo(),
            new BlazorEmail(),
            new BlazorFilePicker(),
            new BlazorFileSystem(),
            new BlazorFlashlight(),
            new BlazorGeocoding(),
            new BlazorGeolocation(),
            new BlazorGyroscope(),
            new BlazorHapticFeedback(),
            new BlazorLauncher(),
            new BlazorMagnetometer(),
            new BlazorMap(browser),
            new BlazorMediaPicker(),
            new BlazorOrientationSensor(),
            new BlazorPhoneDialer(),
            preferences,
            new BlazorScreenshot(),
            () => new BlazorSecureStorage(),
            new BlazorSemanticScreenReader(),
            new BlazorShare(),
            new BlazorSms(),
            new BlazorTextToSpeech(),
            new BlazorVibration(),
            new BlazorWebAuthenticator(),
            () => new BlazorVersionTracking(preferences, appInfo),
            ServiceLifetime.Singleton);

        services.RegisterAvaeEssentials();
    }

    public static (string pattern, string html) UseWebAuthenticator()
    {
        const string html = """
        <!doctype html>
        <html>
        <head><meta charset="utf-8"></head>
        <body>
          <p>Authentication completed. You may close this window.</p>
          <script>
            if (window.opener) {
              window.opener.postMessage(
                { type: "web-authenticator-complete", url: window.location.href },
                window.location.origin
              );
              window.close();
            }
          </script>
        </body>
        </html>
        """;

        return ("/auth/popup-complete", html);
    }

    public static bool IsInitialized =>
        Module != null;

    /// <summary>
    /// Throws when the module has not been imported yet. Used by synchronous API surfaces
    /// that cannot await initialization themselves.
    /// </summary>
    internal static void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "Blazor Essentials is not initialized. Call " +
                "'await BlazorEssentials.InitializeAsync(serviceProvider)' during app startup " +
                "before using Essentials APIs.");
    }

    public static async Task InitializeAsync(IServiceProvider provider)
    {
        Provider = provider;

        var js = provider.GetRequiredService<IJSRuntime>();

        if (Module is not null)
            await Module.DisposeAsync();
        Module = await InvokeCoreAsync(js);
        if (Module == null)
            return;

        var connectivity = (BlazorConnectivity)provider.GetRequiredService<IConnectivity>();
        await connectivity.InitializeAsync();

        var appInfo = (BlazorAppInfo)provider.GetRequiredService<IAppInfo>();
        await appInfo.InitializeAsync();

        var feedback = (BlazorHapticFeedback)provider.GetRequiredService<IHapticFeedback>();
        await feedback.InitializeAsync();

        var preferences = (BlazorPreferences)provider.GetRequiredService<IPreferences>();
        await preferences.InitializeAsync();

        var storage = (BlazorSecureStorage)provider.GetRequiredService<ISecureStorage>();
        await storage.InitializeAsync();

        var battery = (BlazorBattery)provider.GetRequiredService<IBattery>();
        await battery.InitializeAsync();

        var info = (BlazorDeviceInfo)provider.GetRequiredService<IDeviceInfo>();
        await info.InitializeAsync();

        var vibration = (BlazorVibration)provider.GetRequiredService<IVibration>();
        await vibration.InitializeAsync();

        var display = (BlazorDeviceDisplay)provider.GetRequiredService<IDeviceDisplay>();
        await display.InitializeAsync();

        var accelerometer = (BlazorAccelerometer)provider.GetRequiredService<IAccelerometer>();
        await accelerometer.InitializeAsync();

        var gyroscope = (BlazorGyroscope)provider.GetRequiredService<IGyroscope>();
        await gyroscope.InitializeAsync();

        var compass = (BlazorCompass)provider.GetRequiredService<ICompass>();
        await compass.InitializeAsync();

        var orientation = (BlazorOrientationSensor)provider.GetRequiredService<IOrientationSensor>();
        await orientation.InitializeAsync();

        var share = (BlazorShare)provider.GetRequiredService<IShare>();
        await share.InitializeAsync();
    }

    internal static async Task<IJSObjectReference> InvokeCoreAsync(IJSRuntime js)
    {
        if (js == null)
            throw new InvalidOperationException();

        try
        {
            return await js.InvokeAsync<IJSObjectReference>("import", "./BlazorEssentials.js")
                           .ConfigureAwait(false);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null!;
        }
#else
        catch
        {
            return null!;
        }
#endif
    }

    internal  static IJSObjectReference Module { get; set; } = null!;
    internal static IServiceProvider Provider { get; set; } = null!;
}
