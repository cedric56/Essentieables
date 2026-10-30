// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Essentieables;

public interface IAvaeEmail : IEmail
{
    Task ComposeAsync(IEnumerable<FileBase> files, EmailMessage message);
}
