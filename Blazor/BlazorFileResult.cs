using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

internal class BlazorFileResult : FileResult
{
    public byte[] Data { get; private set; }
    public BlazorFileResult(string fullPath, string contentType, byte[] data) : base(fullPath, contentType)
    {
        Data = data;
    }
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

    public new Task<Stream> OpenReadAsync()
    {
        return Task.FromResult<Stream>(File.OpenRead(FullPath));
    }

    public Task<Stream> OpenFileReadAsync()
    {
        return Task.FromResult<Stream>(File.OpenRead(FullPath));
    }
}
