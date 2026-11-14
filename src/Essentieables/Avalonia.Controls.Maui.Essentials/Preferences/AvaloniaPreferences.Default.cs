

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Avalonia.Controls.Maui.Essentials;

public partial class AvaloniaPreferences
{
    static readonly string AppPreferencesPath = GetPreferencesPath();

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
            Console.Error.WriteLine($"Essentieables: preferences could not be read: {ex.Message}");
        }
    }

    partial void SavePreferences()
    {
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
