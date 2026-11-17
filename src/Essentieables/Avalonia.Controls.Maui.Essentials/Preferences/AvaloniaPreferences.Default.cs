

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Avalonia.Controls.Maui.Essentials;

public partial class AvaloniaPreferences
{
    static readonly string AppPreferencesPath = GetPreferencesPath();

    // The file exists but could not be read (locked by an antivirus, OneDrive…): saving now would replace it with the new
    // keys only. Reading is retried before each save; until it works, changes stay in memory.
    bool _unreadable;

    partial void LoadPreferences()
    {
        if (!File.Exists(AppPreferencesPath))
            return;

        try
        {
            System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, string>>? readPreferences;
            using (var stream = File.OpenRead(AppPreferencesPath))
                readPreferences = JsonSerializer.Deserialize(stream, AvaloniaPreferencesJsonSerializerContext.Default.PreferencesDictionary);

            if (readPreferences != null)
            {
                _preferences.Clear();
                foreach (var pair in readPreferences)
                    _preferences.TryAdd(pair.Key, pair.Value);
            }
        }
        catch (JsonException ex)
        {
            // Proceed with empty settings, but keep the unreadable file instead of overwriting it at the next save.
            Essentieables.AtomicFile.SetAside(AppPreferencesPath, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _unreadable = true;
            Console.Error.WriteLine($"Essentieables: preferences could not be read: {ex.Message}");
        }
    }

    /// <summary>Reads the file again after a failed load; the values changed since win. False while it is still unreadable.</summary>
    bool RecoverUnreadable()
    {
        try
        {
            System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, string>>? readPreferences;
            using (var stream = File.OpenRead(AppPreferencesPath))
                readPreferences = JsonSerializer.Deserialize(stream, AvaloniaPreferencesJsonSerializerContext.Default.PreferencesDictionary);
            foreach (var (sharedName, values) in readPreferences ?? new())
            {
                var current = _preferences.GetOrAdd(sharedName, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, string>());
                foreach (var (key, value) in values)
                    current.TryAdd(key, value);
            }
            _unreadable = false;
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _unreadable = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Essentieables: preferences still unreadable, not saved: {ex.Message}");
            return false;
        }
    }

    partial void SavePreferences()
    {
        if (_unreadable && !RecoverUnreadable())
            return;

        // Through a temporary file: a crash while writing never truncates the preferences.
        Essentieables.AtomicFile.Write(AppPreferencesPath, stream =>
            JsonSerializer.Serialize(stream, _preferences, AvaloniaPreferencesJsonSerializerContext.Default.PreferencesDictionary));
    }

    static string GetPreferencesPath()
    {
        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "AvaloniaApp";
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName);

        return Path.Combine(appDataDirectory, "Settings", "preferences.dat");
    }
}
