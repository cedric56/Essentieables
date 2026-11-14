using System;
using System.IO;

namespace Essentieables;

/// <summary>
/// Safe writes of the settings files (preferences, secure storage): a crash while writing never leaves a truncated file, and
/// a file that cannot be read is kept aside instead of being overwritten by the next save.
/// </summary>
internal static class AtomicFile
{
    /// <summary>Writes <paramref name="path"/> through a temporary file of the same folder, then replaces it.</summary>
    public static void Write(string path, Action<Stream> write)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>
    /// Moves an unreadable file to <c>&lt;name&gt;.corrupt-&lt;timestamp&gt;</c>, so the next save starts a new file without
    /// destroying the data, which may still be recovered by hand. Reports it on stderr.
    /// </summary>
    public static void SetAside(string path, Exception reason)
    {
        try
        {
            var backup = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(path, backup, overwrite: true);
            Console.Error.WriteLine($"Essentieables: '{path}' could not be read ({reason.Message}); it was moved to '{backup}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Essentieables: '{path}' could not be read ({reason.Message}) nor moved aside ({ex.Message}).");
        }
    }
}
