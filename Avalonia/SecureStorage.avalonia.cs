// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage;
using SecureStorageDictionary = System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>;

namespace Essentieables;

    [SupportedOSPlatform("windows10.0.10240")]
    partial class OriginalSecureStorageImplementation : ISecureStorage
    {
        internal static readonly string Alias = $"{AppInfo.Current.PackageName}.microsoft.maui.essentials.preferences";

        readonly ISecureStorageImplementation _secureStorage;

        public OriginalSecureStorageImplementation()
        {
            _secureStorage = AppInfoUtils.IsPackagedApp
                ? new OriginalPackagedSecureStorageImplementation()
                : new OriginalUnpackagedSecureStorageImplementation();
        }

        public async Task<string?> GetAsync(string key)
        {
            var encBytes = await _secureStorage.GetAsync(key);

            if (encBytes == null)
                return null;

            var provider = new DataProtectionProvider();

            var buffer = await provider.UnprotectAsync(encBytes.AsBuffer());

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        public async Task SetAsync(string key, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);

            // LOCAL=user and LOCAL=machine do not require enterprise auth capability
            var provider = new DataProtectionProvider("LOCAL=user");

            var buffer = await provider.ProtectAsync(bytes.AsBuffer());

            var encBytes = buffer.ToArray();

            await _secureStorage.SetAsync(key, encBytes);
        }

        public bool Remove(string key)
        {
            return _secureStorage.Remove(key);
        }

        public void RemoveAll()
        {
            _secureStorage.RemoveAll();
        }
    }

    interface ISecureStorageImplementation
    {
        Task<byte[]?> GetAsync(string key);

        Task SetAsync(string key, byte[] value);

        bool Remove(string key);

        void RemoveAll();
    }
    [SupportedOSPlatform("windows10.0.10240")]
    class OriginalPackagedSecureStorageImplementation : ISecureStorageImplementation
    {
        public Task<byte[]?> GetAsync(string key)
        {
            var settings = GetSettings(OriginalSecureStorageImplementation.Alias);
            var encBytes = settings.Values[key] as byte[];
            return Task.FromResult(encBytes);
        }

        public Task SetAsync(string key, byte[] data)
        {
            var settings = GetSettings(OriginalSecureStorageImplementation.Alias);
            settings.Values[key] = data;
            return Task.CompletedTask;
        }

        public bool Remove(string key)
        {
            var settings = GetSettings(OriginalSecureStorageImplementation.Alias);
            return settings.Values.Remove(key);
        }

        public void RemoveAll()
        {
            var settings = GetSettings(OriginalSecureStorageImplementation.Alias);
            settings.Values.Clear();
        }

        static ApplicationDataContainer GetSettings(string name)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Containers.ContainsKey(name))
                localSettings.CreateContainer(name, ApplicationDataCreateDisposition.Always);
            return localSettings.Containers[name];
        }
}

class OriginalUnpackagedSecureStorageImplementation
    : ISecureStorageImplementation
{
    private static readonly string AppSecureStoragePath =
        Path.Combine(
            FileSystem.AppDataDirectory,
            "Settings",
            "securestorage.dat");

    private readonly object _gate = new();
    private readonly SecureStorageDictionary _secureStorage = new();

    public OriginalUnpackagedSecureStorageImplementation()
    {
        Load();
    }

    private void Load()
    {
        lock (_gate)
        {
            if (!File.Exists(AppSecureStoragePath))
                return;

            try
            {
                using var stream = File.OpenRead(AppSecureStoragePath);

                var loaded = JsonSerializer.Deserialize(
                    stream,
                    SecureStorageJsonSerializerContext.Default
                        .SecureStorageDictionary);

                if (loaded is null)
                    return;

                _secureStorage.Clear();

                foreach (var pair in loaded)
                    _secureStorage[pair.Key] = pair.Value;
            }
            catch (JsonException)
            {
                // Preserve the original file. Do not overwrite corrupt data
                // until the application has a recovery strategy.
            }
            catch (IOException)
            {
                // Handle inaccessible storage according to your API policy.
            }
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(AppSecureStoragePath);
        if (directory is null)
            throw new IOException("Secure storage directory is unavailable.");

        Directory.CreateDirectory(directory);

        var tempPath = AppSecureStoragePath + "." +
                       Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                JsonSerializer.Serialize(
                    stream,
                    _secureStorage,
                    SecureStorageJsonSerializerContext.Default
                        .SecureStorageDictionary);

                stream.Flush(flushToDisk: true);
            }

            // Same-directory replacement avoids exposing a partially
            // serialized destination file.
            File.Move(tempPath, AppSecureStoragePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup of abandoned temporary files.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup.
            }
        }
    }

    public Task<byte[]?> GetAsync(string key)
    {
        lock (_gate)
        {
            _secureStorage.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }
    }

    public Task SetAsync(string key, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            _secureStorage[key] = value;
            Save();
        }

        return Task.CompletedTask;
    }

    public bool Remove(string key)
    {
        lock (_gate)
        {
            var removed = _secureStorage.TryRemove(key, out _);

            if (removed)
                Save();

            return removed;
        }
    }

    public void RemoveAll()
    {
        lock (_gate)
        {
            if (_secureStorage.IsEmpty)
                return;

            _secureStorage.Clear();
            Save();
        }
    }
}

[JsonSerializable(typeof(SecureStorageDictionary), TypeInfoPropertyName = nameof(SecureStorageDictionary))]
internal partial class SecureStorageJsonSerializerContext : JsonSerializerContext
{
}
