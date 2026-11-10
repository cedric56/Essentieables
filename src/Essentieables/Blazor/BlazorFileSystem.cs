using Microsoft.JSInterop;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// WebAssembly: <c>/cache</c> and <c>/appdata</c> in the in-memory file system of the browser tab
/// (lost when the tab closes). Server: directories private to the circuit, under the server's temp
/// folder, deleted when the circuit ends. They are never shared between users, and are not durable
/// storage: use Preferences/SecureStorage (browser side) or your own server storage for that.
/// </summary>
internal class BlazorFileSystem : IFileSystem
{
    /// <summary>Largest app package file read by <see cref="OpenAppPackageFileAsync"/> (100 MB).</summary>
    public static long MaxAppPackageFileSize { get; set; } = 100L * 1024 * 1024;

    public string CacheDirectory => GetCacheDirectory();

    public string AppDataDirectory => GetDirectory("appdata");

    internal static string GetCacheDirectory() => GetDirectory("cache");

    static string GetDirectory(string name)
    {
        if (BlazorEssentials.CurrentSession is { } session)
            return session.GetDirectory(name);

        if (BlazorEssentials.IsServer)
            throw new InvalidOperationException(
                "FileSystem was used outside of a Blazor circuit: on Blazor Server, CacheDirectory and AppDataDirectory " +
                "belong to the circuit of the user.");

        // WebAssembly before InitializeAsync: the in-memory file system belongs to this tab anyway.
        var path = "/" + name;
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task<Stream> OpenAppPackageFileAsync(string filename)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);

        await using var data = await BlazorEssentials.Module
            .InvokeAsync<IJSStreamReference?>("fetchAppFile", NormalizePath(filename)).ConfigureAwait(false)
            ?? throw new FileNotFoundException($"App package file '{filename}' was not found at the app base URL.", filename);

        await using var source = await data.OpenReadStreamAsync(MaxAppPackageFileSize).ConfigureAwait(false);
        var copy = new MemoryStream();
        await source.CopyToAsync(copy).ConfigureAwait(false);
        copy.Position = 0;
        return copy;
    }

    public async Task<bool> AppPackageFileExistsAsync(string filename)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        return await BlazorEssentials.Module.InvokeAsync<bool>("appFileExists", NormalizePath(filename)).ConfigureAwait(false);
    }

    static string NormalizePath(string filename) => filename.Replace('\\', '/').TrimStart('/');
}
