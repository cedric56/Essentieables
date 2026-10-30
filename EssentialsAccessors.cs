// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) 2025 AvaloniaUI (Avalonia.Controls.Maui, MIT License). See THIRD-PARTY-NOTICES.txt.

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
using System.Runtime.CompilerServices;

namespace Essentieables;

/// <summary>
/// Installs Avalonia implementations into the Microsoft.Maui.Essentials static facades.
/// The facades only expose internal SetDefault/SetCurrent hooks, so these accessors use
/// <see cref="UnsafeAccessorAttribute"/> instead of MAUI internals.
/// </summary>
public static class EssentialsAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Media.Locale, Microsoft.Maui.Essentials")]
    public extern static object CreateLocale(string language, string country, string name, string id);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetAccelerometer(
        [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Accelerometer, Microsoft.Maui.Essentials")] object? facade,
        IAccelerometer? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetAppActions(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.AppActions, Microsoft.Maui.Essentials")] object? facade,
    IAppActions? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetAppInfo(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.AppInfo, Microsoft.Maui.Essentials")] object? facade,
    IAppInfo? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetBarometer(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Barometer, Microsoft.Maui.Essentials")] object? facade,
    IBarometer? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetBattery(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Battery, Microsoft.Maui.Essentials")] object? facade,
    IBattery? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetBrowser(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Browser, Microsoft.Maui.Essentials")] object? facade,
    IBrowser? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetClipboard(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard, Microsoft.Maui.Essentials")] object? facade,
    IClipboard? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetCompass(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Compass, Microsoft.Maui.Essentials")] object? facade,
    ICompass? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetConnectivity(
    [UnsafeAccessorType("Microsoft.Maui.Networking.Connectivity, Microsoft.Maui.Essentials")] object? facade,
    IConnectivity? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetContacts(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Communication.Contacts, Microsoft.Maui.Essentials")] object? facade,
    IContacts? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetDeviceDisplay(
    [UnsafeAccessorType("Microsoft.Maui.Devices.DeviceDisplay, Microsoft.Maui.Essentials")] object? facade,
    IDeviceDisplay? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetDeviceInfo(
    [UnsafeAccessorType("Microsoft.Maui.Devices.DeviceInfo, Microsoft.Maui.Essentials")] object? facade,
    IDeviceInfo? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetEmail(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Communication.Email, Microsoft.Maui.Essentials")] object? facade,
    IEmail? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetFlashlight(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Flashlight, Microsoft.Maui.Essentials")] object? facade,
    IFlashlight? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetVersionTracking(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.VersionTracking, Microsoft.Maui.Essentials")] object? facade,
    IVersionTracking? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetGeocoding(
        [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Geocoding, Microsoft.Maui.Essentials")] object? facade,
        IGeocoding? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetGeolocation(
        [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Geolocation, Microsoft.Maui.Essentials")] object? facade,
        IGeolocation? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetGyroscope(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Gyroscope, Microsoft.Maui.Essentials")] object? facade,
    IGyroscope? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetLauncher(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Launcher, Microsoft.Maui.Essentials")] object? facade,
    ILauncher? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetMagnetometer(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.Magnetometer, Microsoft.Maui.Essentials")] object? facade,
    IMagnetometer? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetMap(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Map, Microsoft.Maui.Essentials")] object? facade,
    IMap? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetOrientationSensor(
        [UnsafeAccessorType("Microsoft.Maui.Devices.Sensors.OrientationSensor, Microsoft.Maui.Essentials")] object? facade,
        IOrientationSensor? implementation);
    //null,

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetPhoneDialer(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Communication.PhoneDialer, Microsoft.Maui.Essentials")] object? facade,
    IPhoneDialer? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetSecureStorage(
    [UnsafeAccessorType("Microsoft.Maui.Storage.SecureStorage, Microsoft.Maui.Essentials")] object? facade,
    ISecureStorage? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetSemanticScreenReader(
    [UnsafeAccessorType("Microsoft.Maui.Accessibility.SemanticScreenReader, Microsoft.Maui.Essentials")] object? facade,
    ISemanticScreenReader? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetShare(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.DataTransfer.Share, Microsoft.Maui.Essentials")] object? facade,
    IShare? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetSms(
    [UnsafeAccessorType("Microsoft.Maui.ApplicationModel.Communication.Sms, Microsoft.Maui.Essentials")] object? facade,
    ISms? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetTextToSpeech(
    [UnsafeAccessorType("Microsoft.Maui.Media.TextToSpeech, Microsoft.Maui.Essentials")] object? facade,
    ITextToSpeech? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetVibration(
    [UnsafeAccessorType("Microsoft.Maui.Devices.Vibration, Microsoft.Maui.Essentials")] object? facade,
    IVibration? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetFilePicker(
        [UnsafeAccessorType("Microsoft.Maui.Storage.FilePicker, Microsoft.Maui.Essentials")] object? facade,
        IFilePicker? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetCurrent")]
    public static extern void SetFileSystem(
        [UnsafeAccessorType("Microsoft.Maui.Storage.FileSystem, Microsoft.Maui.Essentials")] object? facade,
        IFileSystem? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetHapticFeedback(
        [UnsafeAccessorType("Microsoft.Maui.Devices.HapticFeedback, Microsoft.Maui.Essentials")] object? facade,
        IHapticFeedback? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetMediaPicker(
        [UnsafeAccessorType("Microsoft.Maui.Media.MediaPicker, Microsoft.Maui.Essentials")] object? facade,
        IMediaPicker? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetPreferences(
        [UnsafeAccessorType("Microsoft.Maui.Storage.Preferences, Microsoft.Maui.Essentials")] object? facade,
        IPreferences? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetScreenshot(
        [UnsafeAccessorType("Microsoft.Maui.Media.Screenshot, Microsoft.Maui.Essentials")] object? facade,
        IScreenshot? implementation);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetDefault")]
    public static extern void SetWebAuthenticator(
        [UnsafeAccessorType("Microsoft.Maui.Authentication.WebAuthenticator, Microsoft.Maui.Essentials")] object? facade,
        IWebAuthenticator? implementation);
}
