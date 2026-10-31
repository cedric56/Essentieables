// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Avalonia;
using Avalonia.Headless;
using Essentieables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

AppBuilder.Configure<SmokeApp>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
    .SetupWithoutStarting();

var services = new ServiceCollection();
services.UseEssentieables();
using var provider = services.BuildServiceProvider();

var failures = new List<string>();
void Check(string name, Func<bool> condition)
{
    try
    {
        if (condition())
            Console.WriteLine($"ok   {name}");
        else
            failures.Add(name);
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
    }
}

var key = "smoke-" + Guid.NewGuid().ToString("N");
Check("Preferences round-trip", () =>
{
    Preferences.Default.Set(key, 42);
    var value = Preferences.Default.Get(key, 0);
    Preferences.Default.Remove(key);
    return value == 42;
});
Check("Preferences resolved from DI is the static default", () => ReferenceEquals(provider.GetRequiredService<IPreferences>(), Preferences.Default));
Check("FileSystem.AppDataDirectory exists", () => Directory.Exists(FileSystem.AppDataDirectory));
Check("FileSystem.CacheDirectory exists", () => Directory.Exists(FileSystem.CacheDirectory));
Check("FilePicker is installed", () => FilePicker.Default is not null);
Check("MediaPicker is installed", () => Microsoft.Maui.Media.MediaPicker.Default is not null);

if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
{
    Check("AppInfo.Name", () => !string.IsNullOrEmpty(AppInfo.Current.Name));
    Check("VersionTracking", () => VersionTracking.Default.CurrentVersion is not null);
}

foreach (var failure in failures)
    Console.Error.WriteLine($"FAIL {failure}");

return failures.Count == 0 ? 0 : 1;

class SmokeApp : Application
{
}
