using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Essentieables;

public interface IAvaeEmail : IEmail
{
    Task ComposeAsync(IEnumerable<FileBase> files, EmailMessage message);
}
