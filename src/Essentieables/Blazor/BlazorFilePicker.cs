using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// File picker backed by a hidden &lt;input type="file"&gt; element. The picked files are streamed
/// (no byte[] round-trip, not limited by the SignalR message size on Blazor Server) into a
/// directory of the session cache, so the returned <see cref="FileResult"/> paths are readable
/// with regular System.IO APIs. On Blazor Server that directory is private to the circuit and
/// deleted when the circuit ends.
/// <para>
/// <see cref="PickOptions.FileTypes"/>: give an entry for <c>DevicePlatform.Create("Browser")</c>
/// with extensions (".png") and/or MIME types ("image/*"). Without one, the extensions and MIME types
/// of the other platforms (WinUI, Android, MacCatalyst…) are used.
/// </para>
/// </summary>
internal sealed class BlazorFilePicker : IFilePicker
{
    /// <summary>Largest file accepted, in bytes (100 MB by default). Larger files throw <see cref="IOException"/>.</summary>
    public static long MaxFileSize { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// Largest total of one pick (several files), in bytes (200 MB by default), so a multiple pick cannot fill the server's
    /// disk on Blazor Server. Larger picks throw <see cref="IOException"/> before anything is copied.
    /// </summary>
    public static long MaxTotalSize { get; set; } = 200L * 1024 * 1024;

    internal sealed record PickedFile(string Name, string? Type, long Size);
    internal sealed record PickResult(int Token, PickedFile[] Files);

    public async Task<FileResult?> PickAsync(PickOptions? options = null)
        => (await PickCoreAsync(options?.PickerTitle, GetAccept(options?.FileTypes), multiple: false).ConfigureAwait(false)).FirstOrDefault();

    public async Task<IEnumerable<FileResult>?> PickMultipleAsync(PickOptions? options = null)
        => await PickCoreAsync(options?.PickerTitle, GetAccept(options?.FileTypes), multiple: true).ConfigureAwait(false);

    static readonly DevicePlatform[] FallbackPlatforms =
    [
        DevicePlatform.WinUI, DevicePlatform.Android, DevicePlatform.MacCatalyst, DevicePlatform.macOS,
        DevicePlatform.iOS, DevicePlatform.Tizen,
    ];

    /// <summary>Converts MAUI file types to the value of the HTML <c>accept</c> attribute (null: everything).</summary>
    internal static string? GetAccept(FilePickerFileType? fileTypes)
    {
        if (fileTypes is null)
            return null;

        var browser = TryGetPlatformFileType(fileTypes, BlazorDeviceInfo.BrowserPlatform);
        var values = browser ?? FallbackPlatforms.SelectMany(p => TryGetPlatformFileType(fileTypes, p) ?? []);

        var filters = values
            .Select(NormalizeFilter)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return filters.Count == 0 ? null : string.Join(",", filters);
    }

    /// <summary>".png", "png", "*.png" → ".png"; "image/png", "image/*" kept; anything else (UTTypes…) dropped.</summary>
    internal static string? NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        value = value.Trim();
        if (value.StartsWith("*.", StringComparison.Ordinal))
            value = value[1..];
        if (value.Contains('/'))
            return value.Count(c => c == '/') == 1 && !value.Any(char.IsWhiteSpace) ? value.ToLowerInvariant() : null;
        if (!value.StartsWith('.'))
            value = "." + value;
        // A UTType such as "public.image" is not an extension.
        return value.Length > 1 && value.IndexOf('.', 1) < 0 && value.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '+')
            ? value.ToLowerInvariant()
            : null;
    }

    // GetPlatformFileType is protected: part of the public API contract, unlike the private dictionary.
    static readonly MethodInfo? GetPlatformFileTypeMethod = typeof(FilePickerFileType).GetMethod(
        "GetPlatformFileType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
        binder: null, types: [typeof(DevicePlatform)], modifiers: null);

    static IEnumerable<string>? TryGetPlatformFileType(FilePickerFileType fileTypes, DevicePlatform platform)
    {
        try
        {
            return GetPlatformFileTypeMethod?.Invoke(fileTypes, [platform]) as IEnumerable<string>;
        }
        catch (TargetInvocationException)
        {
            return null; // no entry for this platform
        }
    }

    public static Task<IEnumerable<FileResult>> PickCoreAsync(MediaPickerOptions? options, bool multiple, string accept)
        => PickCoreAsync(options?.Title, accept, multiple);

    public static async Task<IEnumerable<FileResult>> PickCoreAsync(string? title, string? accept, bool multiple)
    {
        var module = BlazorEssentials.Module;

        // Null when the user closes the dialog without choosing anything: MAUI returns null / no file.
        var picked = await module.InvokeAsync<PickResult?>("pickFiles", title, accept, multiple).ConfigureAwait(false);
        if (picked is null || picked.Files.Length == 0)
            return [];

        var directory = Path.Combine(BlazorFileSystem.GetCacheDirectory(), "filepicker", Guid.NewGuid().ToString("N"));
        var results = new List<FileResult>(picked.Files.Length);
        try
        {
            // Checked before copying anything: the sizes come from the browser, the copy itself is still capped per file.
            var total = 0L;
            foreach (var file in picked.Files)
            {
                if (file.Size > MaxFileSize)
                    throw new IOException(
                        $"'{file.Name}' is {file.Size} bytes, more than BlazorFilePicker.MaxFileSize ({MaxFileSize} bytes).");
                total += file.Size;
            }
            if (total > MaxTotalSize)
                throw new IOException($"The files picked total {total} bytes, more than BlazorFilePicker.MaxTotalSize ({MaxTotalSize} bytes).");

            Directory.CreateDirectory(directory);
            for (var i = 0; i < picked.Files.Length; i++)
            {
                var file = picked.Files[i];
                if (file.Size > MaxFileSize)
                    throw new IOException(
                        $"'{file.Name}' is {file.Size} bytes, more than BlazorFilePicker.MaxFileSize ({MaxFileSize} bytes).");

                var path = GetSafePath(directory, file.Name);
                await using (var data = await module.InvokeAsync<IJSStreamReference>("pickedFileData", picked.Token, i).ConfigureAwait(false))
                await using (var source = await data.OpenReadStreamAsync(MaxFileSize).ConfigureAwait(false))
                await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await source.CopyToAsync(target).ConfigureAwait(false);
                }

                var contentType = string.IsNullOrEmpty(file.Type)
                    ? ContentTypeResolver.Resolve(Path.GetFileName(path))
                    : file.Type;

                results.Add(new BlazorFileResult(path, contentType));
            }
            return results;
        }
        catch
        {
            TryDeleteDirectory(directory);
            throw;
        }
        finally
        {
            try
            {
                await module.InvokeVoidAsync("releasePickedFiles", picked.Token).ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    /// <summary>
    /// Reduces a browser-supplied name to a plain file name inside <paramref name="directory"/>
    /// (no separators, "..", reserved characters) and makes it unique.
    /// </summary>
    internal static string GetSafePath(string directory, string? name)
    {
        // Browsers send a bare name, but nothing on the wire guarantees it: strip any path part.
        var fileName = (name ?? string.Empty).Replace('\\', '/');
        fileName = fileName[(fileName.LastIndexOf('/') + 1)..];

        var invalid = Path.GetInvalidFileNameChars();
        fileName = new string(fileName.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (fileName.Length == 0 || fileName is "." or "..")
            fileName = "file";
        if (fileName.Length > 128)
        {
            var ext = Path.GetExtension(fileName);
            if (ext.Length > 16)
                ext = string.Empty;
            fileName = fileName[..(128 - ext.Length)] + ext;
        }

        var root = Path.GetFullPath(directory);
        var candidate = Path.GetFullPath(Path.Combine(root, fileName));
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var n = 1; File.Exists(candidate); n++)
            candidate = Path.GetFullPath(Path.Combine(root, $"{stem} ({n}){extension}"));

        if (!string.Equals(Path.GetDirectoryName(candidate), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
            throw new IOException($"Invalid file name '{name}'.");
        return candidate;
    }

    static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
