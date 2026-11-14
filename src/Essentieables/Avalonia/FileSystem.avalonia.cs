// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Essentieables
{
    /// <summary>
    /// The Windows file system of MAUI and Avae.Windows.Essentials: <c>%LOCALAPPDATA%\&lt;Publisher&gt;\&lt;Package&gt;\Data</c> and
    /// <c>…\Cache</c> (the ApplicationData folders when packaged), so the files of apps migrated from Avae.Windows.Essentials
    /// are found again. App package files are read from the app folder, then from the Avalonia resources (<c>avares://</c>).
    /// </summary>
    [SupportedOSPlatform("windows10.0.10240")]
    partial class OriginalFileSystemImplementation : IFileSystem
    {
        readonly IFileSystem? _assets;

        /// <param name="assets">Where app package files missing from the app folder are looked for (Avalonia resources).</param>
        public OriginalFileSystemImplementation(IFileSystem? assets = null)
        {
            _assets = assets;
        }

        private readonly Lazy<string> _platformCacheDirectory = new(valueFactory: () =>
        {
            if (AppInfoUtils.IsPackagedApp)
            {
                return ApplicationData.Current.LocalCacheFolder.Path;
            }
            else
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppSpecificPath, "Cache");

                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        });

        private readonly Lazy<string> _platformAppDataDirectory = new(valueFactory: () =>
        {
            if (AppInfoUtils.IsPackagedApp)
            {
                return ApplicationData.Current.LocalFolder.Path;
            }
            else
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppSpecificPath, "Data");

                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        });

        static string CleanPath(string path) =>
            string.Join("_", path.Split(Path.GetInvalidFileNameChars()));

        static string AppSpecificPath =>
            Path.Combine(CleanPath(AvaeAppInfo.PublisherName), CleanPath(AvaeAppInfo.PackagedName));

        public string CacheDirectory => _platformCacheDirectory.Value;

        public string AppDataDirectory => _platformAppDataDirectory.Value;

        public Task<Stream> OpenAppPackageFileAsync(string filename)
        {
            if (filename == null)
                throw new ArgumentNullException(nameof(filename));

            var file = OriginalFileSystemUtils.PlatformGetFullAppPackageFilePath(filename);
            if (!File.Exists(file) && _assets is not null)
                return _assets.OpenAppPackageFileAsync(filename);

            if (AppInfoUtils.IsPackagedApp)
            {
                filename = OriginalFileSystemUtils.NormalizePath(filename);

                return Package.Current.InstalledLocation.OpenStreamForReadAsync(filename);
            }
            else
            {
                return Task.FromResult((Stream)File.OpenRead(file));
            }
        }

        public async Task<bool> AppPackageFileExistsAsync(string filename)
        {
            var file = OriginalFileSystemUtils.PlatformGetFullAppPackageFilePath(filename);
            if (File.Exists(file))
                return true;
            return _assets is not null && await _assets.AppPackageFileExistsAsync(filename).ConfigureAwait(false);
        }
    }
}
#endif
