// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
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
            var file = PlatformGetFullAppPackageFilePath(filename);
            return File.Exists(file);
        }

        public static string PlatformGetFullAppPackageFilePath(string filename)
        {
            if (filename is null)
                throw new ArgumentNullException(nameof(filename));

            filename = NormalizePath(filename);
            return Path.Combine(AppInfoUtils.PlatformGetFullAppPackageFilePath, filename);
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
