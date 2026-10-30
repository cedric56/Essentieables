// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.JSInterop;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

internal class BlazorFileSystem : IFileSystem
{
    public string CacheDirectory => EnsureDirectory("/cache");

    public string AppDataDirectory => EnsureDirectory("/appdata");

    static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task<Stream> OpenAppPackageFileAsync(string filename)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(filename);
            var base64 = await BlazorEssentials.Module.InvokeAsync<string?>("fetchAppFile", NormalizePath(filename)).ConfigureAwait(false)
                ?? throw new FileNotFoundException($"App package file '{filename}' was not found at the app base URL.", filename);
            return new MemoryStream(Convert.FromBase64String(base64));
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null!;
        }
#else
        catch
        {
            return null!;
        }
#endif
    }

    public async Task<bool> AppPackageFileExistsAsync(string filename)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(filename);
            return await BlazorEssentials.Module.InvokeAsync<bool>("appFileExists", NormalizePath(filename)).ConfigureAwait(false);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
#else
        catch
        {
            return false;
        }
#endif
    }

    static string NormalizePath(string filename) => filename.Replace('\\', '/').TrimStart('/');
}
