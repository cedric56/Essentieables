// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Essentieables;
using Avalonia.Controls.Maui.Essentials;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Essentieables;

internal partial class AvaeEmail : IAvaeEmail
{
    private const string TempFilePrefix = "avaemail-";
    private static readonly TimeSpan TempFileLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan StaleFileLifetime = TimeSpan.FromDays(1);

    private static async Task PlatformComposeAsync(EmailMessage? message)
    {
        CleanupStaleEmlFiles();

        var content = await GetUri(message);
        var isMailTo = content.StartsWith(
            "mailto:", StringComparison.OrdinalIgnoreCase);

        string target = content;
        string? tempPath = null;

        if (!isMailTo)
        {
            tempPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"{TempFilePrefix}{Guid.NewGuid():N}.eml");

            await File.WriteAllTextAsync(tempPath, content);
            target = tempPath;
        }

        try
        {
            ProcessStartInfo? info = null;

            if (OperatingSystem.IsWindows())
            {
                info = new ProcessStartInfo(target)
                {
                    UseShellExecute = true
                };
            }
            else if (OperatingSystem.IsLinux())
            {
                info = new ProcessStartInfo("xdg-open")
                {
                    UseShellExecute = false
                };
                info.ArgumentList.Add(target);
            }
            else if (OperatingSystem.IsMacOS())
            {
                info = new ProcessStartInfo("open")
                {
                    UseShellExecute = false
                };
                info.ArgumentList.Add(target);
            }

            if (info is null)
                throw new PlatformNotSupportedException(
                    "Email composition is not supported on this platform.");

            // Launch the handler without waiting for it to exit.
            // Disposing the Process object does not terminate the process.
            using var process = Process.Start(info);

            if (process is null)
                throw new InvalidOperationException(
                    "Unable to launch the email application.");
        }
        finally
        {
            if (tempPath is not null)
                ScheduleTempFileCleanup(tempPath);
        }
    }

    private static void ScheduleTempFileCleanup(string path)
    {
        _ = DeleteTempFileLaterAsync(path);
    }

    private static async Task DeleteTempFileLaterAsync(string path)
    {
        try
        {
            await Task.Delay(TempFileLifetime).ConfigureAwait(false);

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // The file may still be in use. A later stale-file sweep
                // can retry cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // A later stale-file sweep can retry cleanup.
            }
        }
        catch (Exception)
        {
            // Cleanup is best-effort and must not become an unobserved
            // fire-and-forget task failure.
        }
    }

    private static void CleanupStaleEmlFiles()
    {
        try
        {
            var directory = FileSystem.CacheDirectory;
            var cutoff = DateTime.UtcNow - StaleFileLifetime;

            foreach (var path in Directory.EnumerateFiles(
                         directory, $"{TempFilePrefix}*.eml"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                        File.Delete(path);
                }
                catch (IOException)
                {
                    // Skip files that are currently unavailable.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files that cannot currently be deleted.
                }
            }
        }
        catch (IOException)
        {
            // Cache cleanup is best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Cache cleanup is best-effort.
        }
    }

    public bool IsComposeSupported => true;

    public Task ComposeAsync(EmailMessage? message) =>
        PlatformComposeAsync(message);

    internal static async Task<string> GetUri(EmailMessage? message)
    {
        if (message is null)
            return "mailto:";

        if (message.BodyFormat == EmailBodyFormat.PlainText &&
            (message.Attachments is null || message.Attachments.Count == 0))
        {
            return await message.ConvertToMailTo();
        }

        return await message.ConvertToEml();
    }

    public Task ComposeAsync(IEnumerable<FileBase> files, EmailMessage message)
    {
        var attachments = new List<EmailAttachment>();

        foreach (var file in files ?? [])
        {
            if (file is AvaloniaFileResult result)
                attachments.Add(new AvaeEmailAttachment(result));
            else
                attachments.Add(new EmailAttachment(file.FullPath));
        }

        message.Attachments = attachments;
        return ComposeAsync(message);
    }
}