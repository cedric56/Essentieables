// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Versioning;

namespace Essentieables
{
    [SupportedOSPlatform("windows10.0.10240")]
    static partial class OriginalFileSystemUtils
    {
        public static string NormalizePath(string filename) =>
            filename
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

        public static bool AppPackageFileExists(string filename)
        {
            string file;
            try
            {
                file = PlatformGetFullAppPackageFilePath(filename);
            }
            catch (ArgumentException)
            {
                // Outside the app package: not one of its files.
                return false;
            }
            return File.Exists(file);
        }

        public static string PlatformGetFullAppPackageFilePath(string filename)
        {
            if (filename is null)
                throw new ArgumentNullException(nameof(filename));

            filename = NormalizePath(filename);
            // Path.Combine returns an absolute filename as is, and ".." climbs out of the app folder: both refused.
            var root = Path.GetFullPath(AppInfoUtils.PlatformGetFullAppPackageFilePath);
            var full = Path.GetFullPath(Path.Combine(root, filename));
            var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            if (Path.IsPathRooted(filename) || !full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"'{filename}' is not a path inside the app package.", nameof(filename));
            return full;
        }

        public static bool TryGetAppPackageFileUri(string filename, [NotNullWhen(true)] out string? uri)
        {
            var path = PlatformGetFullAppPackageFilePath(filename);

            if (File.Exists(path))
            {
                if (AppInfoUtils.IsPackagedApp)
                    uri = $"ms-appx:///{filename.Replace('\\', '/')}";
                else
                    uri = $"file:///{path.Replace('\\', '/')}";

                return true;
            }

            uri = null;
            return false;
        }
    }
}
#endif
