// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

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
    // Settings\securestorage.dat next to FileSystem.AppDataDirectory, as in MAUI and Avae.Windows.Essentials
    // (%LOCALAPPDATA%\<Publisher>\<Package>\Settings).
    private static readonly string AppSecureStoragePath =
        Path.GetFullPath(Path.Combine(
            FileSystem.AppDataDirectory,
            "..",
            "Settings",
            "securestorage.dat"));

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
            // Values written by the preview builds of Essentieables are read when the file of MAUI does not exist yet.
            var path = File.Exists(AppSecureStoragePath)
                ? AppSecureStoragePath
                : OriginalUnpackagedPreferencesImplementation.PreviewPath("securestorage.dat");
            if (!File.Exists(path))
                return;

            try
            {
                SecureStorageDictionary? loaded;
                using (var stream = File.OpenRead(path))
                    loaded = JsonSerializer.Deserialize(
                        stream,
                        SecureStorageJsonSerializerContext.Default
                            .SecureStorageDictionary);

                if (loaded is null)
                    return;

                _secureStorage.Clear();

                foreach (var pair in loaded)
                    _secureStorage[pair.Key] = pair.Value;
            }
            catch (JsonException ex)
            {
                // The next save would replace the file with the new keys only: keep the unreadable one aside, so the
                // secrets it holds are not destroyed (they can still be recovered by hand).
                if (path == AppSecureStoragePath)
                    AtomicFile.SetAside(path, ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable for now (locked, permissions): saving would lose its content, so refuse to.
                _unreadable = true;
                Console.Error.WriteLine($"Essentieables: secure storage could not be read: {ex.Message}");
            }
        }
    }

    // Set when the file exists but could not be read: saving would replace it with the new keys only.
    private bool _unreadable;

    private void Save()
    {
        if (_unreadable)
            throw new IOException($"The secure storage file '{AppSecureStoragePath}' could not be read; it is not overwritten.");

        // Through a temporary file of the same folder: a crash while writing never exposes a partially serialized file.
        AtomicFile.Write(AppSecureStoragePath, stream =>
            JsonSerializer.Serialize(
                stream,
                _secureStorage,
                SecureStorageJsonSerializerContext.Default
                    .SecureStorageDictionary));
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
#endif
