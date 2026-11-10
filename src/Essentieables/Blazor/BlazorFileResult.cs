using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// A picked or captured file. It always exists on disk at <see cref="FileBase.FullPath"/> (in the cache
/// directory of the session), so System.IO, Launcher and Share can read it.
/// The portable Microsoft.Maui.Essentials build cannot open files itself: read the content with
/// <c>OpenReadAsync()</c> on this type, or with <c>Extensions.OpenReadAsync(FileBase, true)</c>.
/// </summary>
internal class BlazorFileResult : FileResult, IAvaeFileResult
{
    /// <summary>Optional in-memory copy of the content; null when the file is only on disk.</summary>
    public byte[]? Data { get; private set; }

    public BlazorFileResult(string fullPath, string contentType, byte[]? data = null) : base(fullPath, contentType)
    {
        // FileBase.PlatformGetContentType is not implemented on the portable build: set it up-front.
        ContentType = contentType;
        FileName = Path.GetFileName(fullPath);
        Data = data;
    }

    public new Task<Stream> OpenReadAsync() => OpenFileStreamAsync();

    public Task<Stream> OpenFileStreamAsync() =>
        Task.FromResult<Stream>(Data is { } data
            ? new MemoryStream(data, writable: false)
            : new FileStream(FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
}

internal class BlazorShareFile : ShareFile
{
    public byte[]? Data { get; private set; }

    public BlazorShareFile(BlazorFileResult result)
        : base(result.FullPath, result.ContentType)
    {
        FileName = result.FileName;
        Data = result.Data;
    }

    public new Task<Stream> OpenReadAsync() => OpenFileReadAsync();

    public Task<Stream> OpenFileReadAsync() =>
        Task.FromResult<Stream>(Data is { } data
            ? new MemoryStream(data, writable: false)
            : File.OpenRead(FullPath));
}
