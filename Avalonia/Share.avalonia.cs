// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Essentieables;
using Avalonia.Controls.Maui.Essentials;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalShareImplementation(AvaeTopLevelStateManager platformProvider) : IAvaeShare
{
    public Task RequestAsync(ShareTextRequest request)
    {
        var hwnd = platformProvider.GetActiveWindowHandle(false);
        var dataTransferManager = OriginalDataTransferManagerHelper.GetDataTransferManager(hwnd);

        dataTransferManager.DataRequested += ShareTextHandler;

        OriginalDataTransferManagerHelper.ShowShare(hwnd);

        void ShareTextHandler(DataTransferManager sender, DataRequestedEventArgs e)
        {
            var newRequest = e.Request;

            newRequest.Data.Properties.Title = request.Title ?? AppInfo.Current.Name;

            if (!string.IsNullOrWhiteSpace(request.Text))
            {
                newRequest.Data.SetText(request.Text);
            }

            if (!string.IsNullOrWhiteSpace(request.Uri))
            {
                newRequest.Data.SetWebLink(new Uri(request.Uri));
            }

            dataTransferManager.DataRequested -= ShareTextHandler;
        }

        return Task.CompletedTask;
    }

    public Task RequestAsync(ShareFileRequest request)
    {
        return RequestAsync((ShareMultipleFilesRequest)request);

    }

    public async Task RequestAsync(ShareMultipleFilesRequest request)
    {
        var storageFiles = new List<IStorageFile>();
        foreach (var file in request.Files ?? [])
            storageFiles.Add(await StorageFile.GetFileFromPathAsync(file.FullPath));

        if (storageFiles.Count == 0)
            return;

        var hwnd = platformProvider.GetActiveWindowHandle(false);
        var dataTransferManager = OriginalDataTransferManagerHelper.GetDataTransferManager(hwnd);

        dataTransferManager.DataRequested += ShareTextHandler;

        OriginalDataTransferManagerHelper.ShowShare(hwnd);

        void ShareTextHandler(DataTransferManager sender, DataRequestedEventArgs e)
        {
            var newRequest = e.Request;

            newRequest.Data.SetStorageItems(storageFiles.ToArray());
            newRequest.Data.Properties.Title = request.Title ?? AppInfo.Current.Name;

            dataTransferManager.DataRequested -= ShareTextHandler;
        }
    }

    public Task RequestAsync(string title, IEnumerable<FileBase> files)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentNullException.ThrowIfNull(files, nameof(files));

        // Convert the enumerable to a list to avoid multiple enumeration and get accurate count
        var shareFiles = new List<ShareFile>(files.Count());

        foreach (var file in files)
        {
            // Check if the file is an Avalonia-specific file result
            if (file is AvaloniaFileResult f)
                // Wrap it with the Avalonia adapter for proper handling
                shareFiles.Add(new AvaeShareFile(f));
            else
                // Use standard MAUI ShareFile for regular files
                shareFiles.Add(new ShareFile(file));
        }

        // Execute the native share request with the converted files
        return RequestAsync(new ShareMultipleFilesRequest()
        {
            Title = title,
            Files = shareFiles
        });
    }
}
[SupportedOSPlatform("windows10.0.10240")]
static class OriginalDataTransferManagerHelper
{
    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }

    public static DataTransferManager GetDataTransferManager(IntPtr appWindow)
    {
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        Guid id = new Guid(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);
        IntPtr result;
        result = interop.GetForWindow(appWindow, id);
        DataTransferManager dataTransferManager = MarshalInterface<DataTransferManager>.FromAbi(result);
        return (dataTransferManager);
    }

    public static void ShowShare(IntPtr appWindow)
    {
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        interop.ShowShareUIForWindow(appWindow);
    }
}
