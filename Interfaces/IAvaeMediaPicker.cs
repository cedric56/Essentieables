// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

namespace Essentieables;

public interface IAvaeMediaPicker : IMediaPicker
{
    Task<FileResult?> CaptureAsync(bool isPhoto, MediaPickerOptions? options = null);
}
