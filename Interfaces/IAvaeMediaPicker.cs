using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

namespace Essentieables;

public interface IAvaeMediaPicker : IMediaPicker
{
    Task<FileResult?> CaptureAsync(bool isPhoto, MediaPickerOptions? options = null);
}
