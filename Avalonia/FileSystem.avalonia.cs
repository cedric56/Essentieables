using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Essentieables
{
    [SupportedOSPlatform("windows10.0.10240")]
    partial class OriginalFileSystemImplementation : IFileSystem
    {
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

            if (AppInfoUtils.IsPackagedApp)
            {
                filename = OriginalFileSystemUtils.NormalizePath(filename);

                return Package.Current.InstalledLocation.OpenStreamForReadAsync(filename);
            }
            else
            {
                var file = OriginalFileSystemUtils.PlatformGetFullAppPackageFilePath(filename);
                return Task.FromResult((Stream)File.OpenRead(file));
            }
        }

        public Task<bool> AppPackageFileExistsAsync(string filename)
        {
            var file = OriginalFileSystemUtils.PlatformGetFullAppPackageFilePath(filename);
            return Task.FromResult(File.Exists(file));
        }
    }
}
