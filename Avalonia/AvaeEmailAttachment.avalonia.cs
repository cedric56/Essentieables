// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Avalonia.Controls.Maui.Essentials;
using Avalonia.Platform.Storage;
using Microsoft.Maui.ApplicationModel.Communication;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

class AvaeEmailAttachment : EmailAttachment
{
    private IStorageFile storageFile;
    public AvaeEmailAttachment(AvaloniaFileResult file)
        : base(file.FullPath, file.ContentType)
    {
        FileName = file.FileName;
        storageFile = file.StorageFile;
    }

    public Task<Stream> OpenAvaloniaReadAsync()
    {
        return storageFile.OpenReadAsync();
    }
}
