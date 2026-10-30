using Microsoft.Maui.ApplicationModel;
using NSubstitute;
using System;
using System.Collections.Generic;
using Xunit;

namespace Essentieables.Tests;

/// <summary>
/// The [UnsafeAccessor] hooks target MAUI internals (SetDefault / SetCurrent). If a MAUI update renames one,
/// the call fails at runtime with MissingMethodException: these tests make such an update fail in CI instead.
/// </summary>
[Collection(StaticStateCollection.Name)]
public class EssentialsAccessorsTests
{
    public record AccessorCase(string Name, Type Interface, Func<object> CreateFake, Action<object?> Set, Func<object> Get)
    {
        public override string ToString() => Name;
    }

    static AccessorCase Row<T>(string name, Action<T?> set, Func<T> get) where T : class =>
        new(name, typeof(T), () => Substitute.For<T>(), o => set((T?)o), () => get());

    public static readonly AccessorCase[] Cases =
    [
        Row<global::Microsoft.Maui.Devices.Sensors.IAccelerometer>("SetAccelerometer", i => EssentialsAccessors.SetAccelerometer(null, i), () => global::Microsoft.Maui.Devices.Sensors.Accelerometer.Default),
        Row<global::Microsoft.Maui.ApplicationModel.IAppActions>("SetAppActions", i => EssentialsAccessors.SetAppActions(null, i), () => global::Microsoft.Maui.ApplicationModel.AppActions.Current),
        Row<global::Microsoft.Maui.ApplicationModel.IAppInfo>("SetAppInfo", i => EssentialsAccessors.SetAppInfo(null, i), () => global::Microsoft.Maui.ApplicationModel.AppInfo.Current),
        Row<global::Microsoft.Maui.Devices.Sensors.IBarometer>("SetBarometer", i => EssentialsAccessors.SetBarometer(null, i), () => global::Microsoft.Maui.Devices.Sensors.Barometer.Default),
        Row<global::Microsoft.Maui.Devices.IBattery>("SetBattery", i => EssentialsAccessors.SetBattery(null, i), () => global::Microsoft.Maui.Devices.Battery.Default),
        Row<global::Microsoft.Maui.ApplicationModel.IBrowser>("SetBrowser", i => EssentialsAccessors.SetBrowser(null, i), () => global::Microsoft.Maui.ApplicationModel.Browser.Default),
        Row<global::Microsoft.Maui.ApplicationModel.DataTransfer.IClipboard>("SetClipboard", i => EssentialsAccessors.SetClipboard(null, i), () => global::Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default),
        Row<global::Microsoft.Maui.Devices.Sensors.ICompass>("SetCompass", i => EssentialsAccessors.SetCompass(null, i), () => global::Microsoft.Maui.Devices.Sensors.Compass.Default),
        Row<global::Microsoft.Maui.Networking.IConnectivity>("SetConnectivity", i => EssentialsAccessors.SetConnectivity(null, i), () => global::Microsoft.Maui.Networking.Connectivity.Current),
        Row<global::Microsoft.Maui.ApplicationModel.Communication.IContacts>("SetContacts", i => EssentialsAccessors.SetContacts(null, i), () => global::Microsoft.Maui.ApplicationModel.Communication.Contacts.Default),
        Row<global::Microsoft.Maui.Devices.IDeviceDisplay>("SetDeviceDisplay", i => EssentialsAccessors.SetDeviceDisplay(null, i), () => global::Microsoft.Maui.Devices.DeviceDisplay.Current),
        Row<global::Microsoft.Maui.Devices.IDeviceInfo>("SetDeviceInfo", i => EssentialsAccessors.SetDeviceInfo(null, i), () => global::Microsoft.Maui.Devices.DeviceInfo.Current),
        Row<global::Microsoft.Maui.ApplicationModel.Communication.IEmail>("SetEmail", i => EssentialsAccessors.SetEmail(null, i), () => global::Microsoft.Maui.ApplicationModel.Communication.Email.Default),
        Row<global::Microsoft.Maui.Devices.IFlashlight>("SetFlashlight", i => EssentialsAccessors.SetFlashlight(null, i), () => global::Microsoft.Maui.Devices.Flashlight.Default),
        Row<global::Microsoft.Maui.ApplicationModel.IVersionTracking>("SetVersionTracking", i => EssentialsAccessors.SetVersionTracking(null, i), () => global::Microsoft.Maui.ApplicationModel.VersionTracking.Default),
        Row<global::Microsoft.Maui.Devices.Sensors.IGeocoding>("SetGeocoding", i => EssentialsAccessors.SetGeocoding(null, i), () => global::Microsoft.Maui.Devices.Sensors.Geocoding.Default),
        Row<global::Microsoft.Maui.Devices.Sensors.IGeolocation>("SetGeolocation", i => EssentialsAccessors.SetGeolocation(null, i), () => global::Microsoft.Maui.Devices.Sensors.Geolocation.Default),
        Row<global::Microsoft.Maui.Devices.Sensors.IGyroscope>("SetGyroscope", i => EssentialsAccessors.SetGyroscope(null, i), () => global::Microsoft.Maui.Devices.Sensors.Gyroscope.Default),
        Row<global::Microsoft.Maui.ApplicationModel.ILauncher>("SetLauncher", i => EssentialsAccessors.SetLauncher(null, i), () => global::Microsoft.Maui.ApplicationModel.Launcher.Default),
        Row<global::Microsoft.Maui.Devices.Sensors.IMagnetometer>("SetMagnetometer", i => EssentialsAccessors.SetMagnetometer(null, i), () => global::Microsoft.Maui.Devices.Sensors.Magnetometer.Default),
        Row<global::Microsoft.Maui.ApplicationModel.IMap>("SetMap", i => EssentialsAccessors.SetMap(null, i), () => global::Microsoft.Maui.ApplicationModel.Map.Default),
        Row<global::Microsoft.Maui.Devices.Sensors.IOrientationSensor>("SetOrientationSensor", i => EssentialsAccessors.SetOrientationSensor(null, i), () => global::Microsoft.Maui.Devices.Sensors.OrientationSensor.Default),
        Row<global::Microsoft.Maui.ApplicationModel.Communication.IPhoneDialer>("SetPhoneDialer", i => EssentialsAccessors.SetPhoneDialer(null, i), () => global::Microsoft.Maui.ApplicationModel.Communication.PhoneDialer.Default),
        Row<global::Microsoft.Maui.Storage.ISecureStorage>("SetSecureStorage", i => EssentialsAccessors.SetSecureStorage(null, i), () => global::Microsoft.Maui.Storage.SecureStorage.Default),
        Row<global::Microsoft.Maui.Accessibility.ISemanticScreenReader>("SetSemanticScreenReader", i => EssentialsAccessors.SetSemanticScreenReader(null, i), () => global::Microsoft.Maui.Accessibility.SemanticScreenReader.Default),
        Row<global::Microsoft.Maui.ApplicationModel.DataTransfer.IShare>("SetShare", i => EssentialsAccessors.SetShare(null, i), () => global::Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default),
        Row<global::Microsoft.Maui.ApplicationModel.Communication.ISms>("SetSms", i => EssentialsAccessors.SetSms(null, i), () => global::Microsoft.Maui.ApplicationModel.Communication.Sms.Default),
        Row<global::Microsoft.Maui.Media.ITextToSpeech>("SetTextToSpeech", i => EssentialsAccessors.SetTextToSpeech(null, i), () => global::Microsoft.Maui.Media.TextToSpeech.Default),
        Row<global::Microsoft.Maui.Devices.IVibration>("SetVibration", i => EssentialsAccessors.SetVibration(null, i), () => global::Microsoft.Maui.Devices.Vibration.Default),
        Row<global::Microsoft.Maui.Storage.IFilePicker>("SetFilePicker", i => EssentialsAccessors.SetFilePicker(null, i), () => global::Microsoft.Maui.Storage.FilePicker.Default),
        Row<global::Microsoft.Maui.Storage.IFileSystem>("SetFileSystem", i => EssentialsAccessors.SetFileSystem(null, i), () => global::Microsoft.Maui.Storage.FileSystem.Current),
        Row<global::Microsoft.Maui.Devices.IHapticFeedback>("SetHapticFeedback", i => EssentialsAccessors.SetHapticFeedback(null, i), () => global::Microsoft.Maui.Devices.HapticFeedback.Default),
        Row<global::Microsoft.Maui.Media.IMediaPicker>("SetMediaPicker", i => EssentialsAccessors.SetMediaPicker(null, i), () => global::Microsoft.Maui.Media.MediaPicker.Default),
        Row<global::Microsoft.Maui.Storage.IPreferences>("SetPreferences", i => EssentialsAccessors.SetPreferences(null, i), () => global::Microsoft.Maui.Storage.Preferences.Default),
        Row<global::Microsoft.Maui.Media.IScreenshot>("SetScreenshot", i => EssentialsAccessors.SetScreenshot(null, i), () => global::Microsoft.Maui.Media.Screenshot.Default),
        Row<global::Microsoft.Maui.Authentication.IWebAuthenticator>("SetWebAuthenticator", i => EssentialsAccessors.SetWebAuthenticator(null, i), () => global::Microsoft.Maui.Authentication.WebAuthenticator.Default),
    ];

    public static TheoryData<AccessorCase> All()
    {
        var data = new TheoryData<AccessorCase>();
        foreach (var c in Cases)
            data.Add(c);
        return data;
    }

    [Theory]
    [MemberData(nameof(All))]
    public void Setter_installs_the_implementation_on_the_static_facade(AccessorCase accessor)
    {
        var fake = accessor.CreateFake();
        try
        {
            accessor.Set(fake);
            Assert.Same(fake, accessor.Get());
        }
        finally
        {
            accessor.Set(null);
        }
    }

    [Fact]
    public void Every_accessor_is_covered()
    {
        var declared = typeof(EssentialsAccessors).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var covered = new HashSet<string>(Array.ConvertAll(Cases, c => c.Name));
        foreach (var method in declared)
        {
            if (method.Name.StartsWith("Set", StringComparison.Ordinal))
                Assert.Contains(method.Name, covered);
        }
    }

    [Fact]
    public void CreateLocale_builds_a_maui_locale()
    {
        var locale = EssentialsAccessors.CreateLocale("fr", "FR", "Français", "fr-FR");
        var typed = Assert.IsType<Microsoft.Maui.Media.Locale>(locale);
        Assert.Equal("fr", typed.Language);
        Assert.Equal("FR", typed.Country);
        Assert.Equal("fr-FR", typed.Id);
    }
}
