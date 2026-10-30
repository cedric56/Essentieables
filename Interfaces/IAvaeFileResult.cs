// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

public interface IAvaeFileResult
{
    Task<Stream> OpenFileStreamAsync();
}
