using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

public interface IAvaeFileResult
{
    Task<Stream> OpenFileStreamAsync();
}
