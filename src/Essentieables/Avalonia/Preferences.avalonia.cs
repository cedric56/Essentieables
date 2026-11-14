// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Avalonia.Controls.Maui.Essentials;
using Microsoft.Maui.Storage;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using Windows.Storage;
using PreferencesDictionary = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, string>>;
using ShareNameDictionary = System.Collections.Concurrent.ConcurrentDictionary<string, string>;

namespace Essentieables;

/// <summary>
/// The Windows preferences of MAUI and Avae.Windows.Essentials: <c>ApplicationData.LocalSettings</c> in a packaged (MSIX) app,
/// otherwise <c>Settings\preferences.dat</c> next to <see cref="FileSystem.AppDataDirectory"/>, so the values of apps
/// migrated from Avae.Windows.Essentials are found again.
/// </summary>
[SupportedOSPlatform("windows10.0.10240")]
class OriginalPreferencesImplementation : IPreferences
{
    readonly IPreferences _preferences;

    public OriginalPreferencesImplementation(IFileSystem fileSystem)
    {
        _preferences = AppInfoUtils.IsPackagedApp
            ? new OriginalPackagedPreferencesImplementation()
            : new OriginalUnpackagedPreferencesImplementation(fileSystem);
    }

    public bool ContainsKey(string key, string? sharedName) =>
        _preferences.ContainsKey(key, sharedName);

    public void Remove(string key, string? sharedName) =>
        _preferences.Remove(key, sharedName);

    public void Clear(string? sharedName) =>
        _preferences.Clear(sharedName);

    public void Set<T>(string key, T value, string? sharedName) =>
        _preferences.Set<T>(key, value, sharedName);

    public T Get<T>(string key, T defaultValue, string? sharedName) =>
        _preferences.Get<T>(key, defaultValue, sharedName);

    static readonly Type[] SupportedTypes =
    [
        typeof(string), typeof(int), typeof(bool), typeof(long), typeof(double), typeof(float), typeof(DateTime), typeof(DateTimeOffset),
    ];

    // MAUI's Preferences.CheckIsSupportedType is internal.
    internal static void CheckIsSupportedType<T>()
    {
        if (Array.IndexOf(SupportedTypes, typeof(T)) < 0)
            throw new NotSupportedException($"Preferences using '{typeof(T)}' type is not supported");
    }
}

[SupportedOSPlatform("windows10.0.10240")]
class OriginalPackagedPreferencesImplementation : IPreferences
{
    static readonly object locker = new();

    public bool ContainsKey(string key, string? sharedName)
    {
        lock (locker)
        {
            var appDataContainer = GetApplicationDataContainer(sharedName);
            return appDataContainer.Values.ContainsKey(key);
        }
    }

    public void Remove(string key, string? sharedName)
    {
        lock (locker)
        {
            var appDataContainer = GetApplicationDataContainer(sharedName);
            if (appDataContainer.Values.ContainsKey(key))
                appDataContainer.Values.Remove(key);
        }
    }

    public void Clear(string? sharedName)
    {
        lock (locker)
        {
            var appDataContainer = GetApplicationDataContainer(sharedName);
            appDataContainer.Values.Clear();
        }
    }

    public void Set<T>(string key, T value, string? sharedName)
    {
        OriginalPreferencesImplementation.CheckIsSupportedType<T>();

        lock (locker)
        {
            var appDataContainer = GetApplicationDataContainer(sharedName);

            if (value == null)
            {
                if (appDataContainer.Values.ContainsKey(key))
                    appDataContainer.Values.Remove(key);
                return;
            }

            if (value is DateTime dt)
            {
                appDataContainer.Values[key] = dt.ToBinary();
            }
            else if (value is DateTimeOffset dto)
            {
                appDataContainer.Values[key] = dto.ToString("O");
            }
            else
            {
                appDataContainer.Values[key] = value;
            }
        }
    }

    public T Get<T>(string key, T defaultValue, string? sharedName)
    {
        lock (locker)
        {
            var appDataContainer = GetApplicationDataContainer(sharedName);
            if (appDataContainer.Values.TryGetValue(key, out var tempValue) && tempValue != null)
            {
                if (defaultValue is DateTime)
                {
                    return (T)(object)DateTime.FromBinary((long)tempValue);
                }
                else if (defaultValue is DateTimeOffset)
                {
                    if (DateTimeOffset.TryParse((string)tempValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset))
                        return (T)(object)dateTimeOffset;
                }
                else if (tempValue is T typed)
                {
                    return typed;
                }
                else
                {
                    try
                    {
                        return (T)Convert.ChangeType(tempValue, typeof(T), CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                    {
                        // bad get, fall back to default
                    }
                }
            }
        }

        return defaultValue;
    }

    static ApplicationDataContainer GetApplicationDataContainer(string? sharedName)
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (string.IsNullOrWhiteSpace(sharedName))
            return localSettings;

        if (!localSettings.Containers.ContainsKey(sharedName))
            localSettings.CreateContainer(sharedName, ApplicationDataCreateDisposition.Always);

        return localSettings.Containers[sharedName];
    }
}

[SupportedOSPlatform("windows10.0.10240")]
class OriginalUnpackagedPreferencesImplementation : IPreferences
{
    readonly string _path;
    readonly PreferencesDictionary _preferences = new();
    readonly object _locker = new();

    public OriginalUnpackagedPreferencesImplementation(IFileSystem fileSystem)
    {
        // %LOCALAPPDATA%\<Publisher>\<Package>\Settings\preferences.dat, as in MAUI and Avae.Windows.Essentials.
        _path = Path.GetFullPath(Path.Combine(fileSystem.AppDataDirectory, "..", "Settings", "preferences.dat"));
        Load();
        _preferences.GetOrAdd(string.Empty, _ => new ShareNameDictionary());
    }

    /// <summary>Where the preview builds of Essentieables (Avalonia.Controls.Maui.Essentials) wrote the preferences.</summary>
    internal static string PreviewPath(string fileName)
    {
        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "AvaloniaApp";
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName, "Settings", fileName);
    }

    public bool ContainsKey(string key, string? sharedName = null)
        => _preferences.TryGetValue(CleanSharedName(sharedName), out var inner) && inner.ContainsKey(key);

    public void Remove(string key, string? sharedName = null)
    {
        if (_preferences.TryGetValue(CleanSharedName(sharedName), out var inner))
        {
            inner.TryRemove(key, out _);
            Save();
        }
    }

    public void Clear(string? sharedName = null)
    {
        if (_preferences.TryGetValue(CleanSharedName(sharedName), out var prefs))
        {
            prefs.Clear();
            Save();
        }
    }

    public void Set<T>(string key, T value, string? sharedName = null)
    {
        OriginalPreferencesImplementation.CheckIsSupportedType<T>();

        var prefs = _preferences.GetOrAdd(CleanSharedName(sharedName), _ => new ShareNameDictionary());

        if (value is null)
            prefs.TryRemove(key, out _);
        else if (value is DateTime dt)
            prefs[key] = string.Format(CultureInfo.InvariantCulture, "{0}", dt.ToBinary());
        else if (value is DateTimeOffset dto)
            prefs[key] = dto.ToString("O");
        else
            prefs[key] = string.Format(CultureInfo.InvariantCulture, "{0}", value);

        Save();
    }

    public T Get<T>(string key, T defaultValue, string? sharedName = null)
    {
        if (_preferences.TryGetValue(CleanSharedName(sharedName), out var inner)
            && inner.TryGetValue(key, out var value) && value is not null)
        {
            if (defaultValue is DateTime)
            {
                // long for the .NET 9+ format
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                    return (T)(object)DateTime.FromBinary(longValue);
                // DateTime string for the .NET 8 format
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var datetimeValue))
                    return (T)(object)datetimeValue;
            }
            else if (defaultValue is DateTimeOffset)
            {
                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset))
                    return (T)(object)dateTimeOffset;
            }
            else
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                {
                    // bad get, fall back to default
                }
            }
        }

        return defaultValue;
    }

    void Load()
    {
        // Values written by the preview builds of Essentieables are read when the file of MAUI does not exist yet.
        var path = File.Exists(_path) ? _path : PreviewPath("preferences.dat");
        if (!File.Exists(path))
            return;

        try
        {
            using var stream = File.OpenRead(path);
            var readPreferences = JsonSerializer.Deserialize(stream, AvaloniaPreferencesJsonSerializerContext.Default.PreferencesDictionary);

            if (readPreferences != null)
            {
                _preferences.Clear();
                foreach (var pair in readPreferences)
                    _preferences.TryAdd(pair.Key, pair.Value);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // if deserialization fails proceed with empty settings
        }
    }

    void Save()
    {
        lock (_locker)
        {
            var dir = Path.GetDirectoryName(_path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            using var stream = File.Create(_path);
            JsonSerializer.Serialize(stream, _preferences, AvaloniaPreferencesJsonSerializerContext.Default.PreferencesDictionary);
        }
    }

    static string CleanSharedName(string? sharedName) =>
        string.IsNullOrEmpty(sharedName) ? string.Empty : sharedName;
}
#endif
