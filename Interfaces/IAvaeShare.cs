using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Essentieables;

public partial interface IAvaeShare : IShare
{
    Task RequestAsync(string title, IEnumerable<FileBase> files);
}
