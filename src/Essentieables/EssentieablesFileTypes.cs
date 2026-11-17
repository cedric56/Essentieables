using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System.Collections.Generic;

namespace Essentieables;

/// <summary>
/// The file types of <see cref="FilePickerFileType.Images"/> & co, usable everywhere. On the portable build of MAUI
/// (net10.0: Blazor, Avalonia.Browser, Avalonia on Linux and macOS, plain net10.0 apps), MAUI cannot build its predefined
/// types: <c>FilePickerFileType.Images</c> and <c>PickOptions.Images</c> throw <c>NotImplementedInReferenceAssemblyException</c>.
/// These have an entry for every platform, the browser included:
/// <code>await FilePicker.Default.PickAsync(EssentieablesPickOptions.Images);</code>
/// </summary>
public static class EssentieablesFileTypes
{
    /// <summary>The platform of the browser implementations (Blazor, Avalonia.Browser).</summary>
    public static DevicePlatform Browser { get; } = DevicePlatform.Create("Browser");

    static readonly DevicePlatform Linux = DevicePlatform.Create("Linux");

    public static FilePickerFileType Images { get; } = Create(
        [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".heic", ".heif", ".tif", ".tiff", ".avif"],
        ["image/*"],
        ["public.image"]);

    public static FilePickerFileType Png { get; } = Create([".png"], ["image/png"], ["public.png"]);

    public static FilePickerFileType Jpeg { get; } = Create([".jpg", ".jpeg"], ["image/jpeg"], ["public.jpeg"]);

    public static FilePickerFileType Videos { get; } = Create(
        [".mp4", ".mov", ".m4v", ".avi", ".wmv", ".mkv", ".webm", ".3gp"],
        ["video/*"],
        ["public.movie"]);

    public static FilePickerFileType Pdf { get; } = Create([".pdf"], ["application/pdf"], ["com.adobe.pdf"]);

    /// <summary>
    /// Extensions for the desktop pickers (Windows, macOS, Linux), MIME types for Android, uniform type identifiers for iOS and
    /// Mac Catalyst, both extensions and MIME types for the browser (<c>accept</c> attribute).
    /// </summary>
    public static FilePickerFileType Create(string[] extensions, string[] mimeTypes, string[] appleTypes)
    {
        string[] browser = [.. extensions, .. mimeTypes];
        return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [Browser] = browser,
            [DevicePlatform.WinUI] = extensions,
            [DevicePlatform.macOS] = extensions,
            [Linux] = extensions,
            [DevicePlatform.Unknown] = extensions,
            [DevicePlatform.Android] = mimeTypes,
            [DevicePlatform.Tizen] = mimeTypes,
            [DevicePlatform.iOS] = appleTypes,
            [DevicePlatform.MacCatalyst] = appleTypes,
        });
    }
}

/// <summary>The <see cref="PickOptions"/> of <see cref="PickOptions.Images"/> & co, usable on the portable MAUI build too.</summary>
public static class EssentieablesPickOptions
{
    public static PickOptions Images => new() { FileTypes = EssentieablesFileTypes.Images };

    public static PickOptions Videos => new() { FileTypes = EssentieablesFileTypes.Videos };

    public static PickOptions Pdf => new() { FileTypes = EssentieablesFileTypes.Pdf };
}
