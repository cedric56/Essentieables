using Microsoft.JSInterop;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// WebAssembly (Blazor and Avalonia.Browser): <c>/_cache</c> and <c>/_appdata</c> in the file system of the browser tab.
/// When the app links Emscripten's IDBFS (<c>&lt;EmccExtraLDFlags&gt;-lidbfs.js&lt;/EmccExtraLDFlags&gt;</c>, wasm-tools
/// workload), both are mounted on IndexedDB by <c>InitializeAsync</c> and survive reloads (<see cref="IsPersistent"/>);
/// otherwise they live in memory and are lost when the tab closes.
/// Server: directories private to the circuit, under the server's temp folder, deleted when the circuit ends.
/// They are never shared between users; on the server they are not durable storage.
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
        var path = "/" + GetBrowserDirectoryName(name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The directory names in the browser: <c>_cache</c> and <c>_appdata</c>, the IndexedDB stores of Avae.Browser.Essentials,
    /// so the files it persisted are found again.
    /// </summary>
    internal static string GetBrowserDirectoryName(string name) => "_" + name;

    static IJSObjectReference? s_persistenceModule;

    /// <summary>
    /// True when <see cref="CacheDirectory"/> and <see cref="AppDataDirectory"/> are kept in IndexedDB (WebAssembly app linked
    /// with IDBFS). Writes are persisted automatically; <see cref="PersistAsync"/> forces it, for instance before navigating away.
    /// </summary>
    public static bool IsPersistent { get; private set; }

    /// <summary>Mounts the directories on IndexedDB and loads their content (WebAssembly only). Called by InitializeAsync.</summary>
    internal static async Task MountPersistentStorageAsync(IJSObjectReference module)
    {
        try
        {
            string[] paths = ["/" + GetBrowserDirectoryName("cache"), "/" + GetBrowserDirectoryName("appdata")];
            IsPersistent = await module.InvokeAsync<bool>("fsMountPersistent", (object)paths).ConfigureAwait(false);
            if (IsPersistent)
                s_persistenceModule = module;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            IsPersistent = false;
        }
    }

    /// <summary>Writes the directories to IndexedDB now. Returns false when they are not persistent or the write failed.</summary>
    public static async Task<bool> PersistAsync()
        => s_persistenceModule is { } module
            && await module.InvokeAsync<bool>("fsPersist").ConfigureAwait(false);

    /// <summary>Also writes the directories to IndexedDB every <paramref name="interval"/> (5 seconds by default).</summary>
    public static async Task StartPeriodicFlushAsync(TimeSpan? interval = null)
    {
        if (s_persistenceModule is { } module)
            await module.InvokeVoidAsync("fsStartPeriodicFlush", (int)(interval ?? TimeSpan.FromSeconds(5)).TotalMilliseconds).ConfigureAwait(false);
    }

    public static async Task StopPeriodicFlushAsync()
    {
        if (s_persistenceModule is { } module)
            await module.InvokeVoidAsync("fsStopPeriodicFlush").ConfigureAwait(false);
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
