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
                // Unreadable for now (locked, permissions): saving would lose its content, so refuse to. (A preview file
                // that cannot be read is not overwritten anyway: the new file has another path.)
                _unreadable = path == AppSecureStoragePath;
                Console.Error.WriteLine($"Essentieables: secure storage could not be read: {ex.Message}");
            }
        }
    }

    // Set when the file exists but could not be read: saving would replace it with the new keys only.
    private bool _unreadable;

    /// <summary>
    /// Reads the file again after a failed load (a lock by an antivirus or a sync client is usually short). Nothing was
    /// changed in memory meanwhile, since every change is refused while the file is unreadable.
    /// </summary>
    private bool TryRecover()
    {
        if (!_unreadable)
            return true;
        try
        {
            SecureStorageDictionary? loaded;
            using (var stream = File.OpenRead(AppSecureStoragePath))
                loaded = JsonSerializer.Deserialize(stream, SecureStorageJsonSerializerContext.Default.SecureStorageDictionary);
            _secureStorage.Clear();
            foreach (var pair in loaded ?? new())
                _secureStorage[pair.Key] = pair.Value;
            _unreadable = false;
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _unreadable = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Essentieables: secure storage still unreadable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies a change and saves it; when the file cannot be read or written, the change is undone and the error thrown,
    /// so a later <see cref="GetAsync"/> never returns a value that was not saved.
    /// </summary>
    private void Commit(Action apply, Action rollback)
    {
        if (!TryRecover())
            throw new IOException($"The secure storage file '{AppSecureStoragePath}' could not be read; it is not overwritten.");

        apply();
        try
        {
            // Through a temporary file of the same folder: a crash while writing never exposes a partially serialized file.
            AtomicFile.Write(AppSecureStoragePath, stream =>
                JsonSerializer.Serialize(
                    stream,
                    _secureStorage,
                    SecureStorageJsonSerializerContext.Default
                        .SecureStorageDictionary));
        }
        catch
        {
            rollback();
            throw;
        }
    }

    public Task<byte[]?> GetAsync(string key)
    {
        lock (_gate)
        {
            TryRecover();
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
            var existed = _secureStorage.TryGetValue(key, out var previous);
            Commit(
                () => _secureStorage[key] = value,
                () =>
                {
                    if (existed)
                        _secureStorage[key] = previous!;
                    else
                        _secureStorage.TryRemove(key, out _);
                });
        }

        return Task.CompletedTask;
    }

    public bool Remove(string key)
    {
        lock (_gate)
        {
            if (!TryRecover() || !_secureStorage.TryGetValue(key, out var previous))
                return TryRemoveWhileUnreadable();

            Commit(() => _secureStorage.TryRemove(key, out _), () => _secureStorage[key] = previous);
            return true;
        }
    }

    // The file could not be read: the key may be in it, but it cannot be removed without losing the other keys.
    private bool TryRemoveWhileUnreadable() =>
        _unreadable
            ? throw new IOException($"The secure storage file '{AppSecureStoragePath}' could not be read; it is not overwritten.")
            : false;

    public void RemoveAll()
    {
        lock (_gate)
        {
            if (!_unreadable && _secureStorage.IsEmpty)
                return;

            // RemoveAll needs no previous content: an unreadable file is replaced by an empty one.
            var snapshot = _secureStorage.ToArray();
            var wasUnreadable = _unreadable;
            _unreadable = false;
            Commit(
                () => _secureStorage.Clear(),
                () =>
                {
                    _unreadable = wasUnreadable;
                    foreach (var pair in snapshot)
                        _secureStorage[pair.Key] = pair.Value;
                });
        }
    }
}

[JsonSerializable(typeof(SecureStorageDictionary), TypeInfoPropertyName = nameof(SecureStorageDictionary))]
internal partial class SecureStorageJsonSerializerContext : JsonSerializerContext
{
}
#endif
