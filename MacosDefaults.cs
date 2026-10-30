using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Essentieables;

public static class MacosDefaults
{
    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaFilePicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateAccelerometer();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaMediaPicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateAppActions();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AppInfoImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateAppInfo();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaFilePicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateBarometer();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.BatteryImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateBattery();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.BrowserImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateBrowser();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.ClipboardImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateClipboard();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaMediaPicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateCompass();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.ConnectivityImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateConnectivity();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaFilePicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateContacts();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.DeviceDisplayImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateDeviceDisplay();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.DeviceInfoImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateDeviceInfo();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.EmailImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateEmail();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.FlashlightImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateFlashlight();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaWebAuthenticator, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateGeocoding();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.GeolocationImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateGeolocation();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaMediaPicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateGyroscope();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.HapticFeedbackImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateHapticFeedback();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.LauncherImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateLauncher();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaMediaPicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateMagnetometer();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.MapImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateMap();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaFilePicker, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateOrientationSensor();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.PhoneDialerImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreatePhoneDialer();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.SecureStorageImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateSecureStorage();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.SemanticScreenReaderImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateSemanticScreenReader();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.ShareImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateShare();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.SmsImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateSms();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.TextToSpeechImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateTextToSpeech();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.VibrationImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateVibration();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.FilePickerImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateFilePicker();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.MediaPickerImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateMediaPicker();

    //[UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    //[return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.AvaloniaWebAuthenticator, Microsoft.Maui.Platforms.MacOS.Essentials")]
    //internal extern static object CreateWebAuthenticator();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.ScreenshotImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateScreenshot();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.PreferencesImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreatePreferences();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.Maui.Platforms.MacOS.Essentials.FileSystemImplementation, Microsoft.Maui.Platforms.MacOS.Essentials")]
    public extern static object CreateFileSystem();
}
