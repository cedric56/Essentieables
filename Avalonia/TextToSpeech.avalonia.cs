// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Essentieables;
using Microsoft.Maui.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.SpeechSynthesis;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalTextToSpeechImplementation : ITextToSpeech
{
    internal const float PitchMax = 2.0f;
    internal const float PitchDefault = 1.0f;
    internal const float PitchMin = 0.0f;

    internal const float VolumeMax = 1.0f;
    internal const float VolumeDefault = 0.5f;
    internal const float VolumeMin = 0.0f;

    SemaphoreSlim? semaphore;

    public Task<IEnumerable<Locale>> GetLocalesAsync() =>
        PlatformGetLocalesAsync();

    public async Task SpeakAsync(string text, SpeechOptions? options = default, CancellationToken cancelToken = default)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentNullException(nameof(text), "Text cannot be null or empty string");

        if (options?.Volume.HasValue ?? false)
        {
            if (options.Volume.Value < VolumeMin || options.Volume.Value > VolumeMax)
                throw new ArgumentOutOfRangeException($"Volume must be >= {VolumeMin} and <= {VolumeMax}");
        }

        if (options?.Pitch.HasValue ?? false)
        {
            if (options.Pitch.Value < PitchMin || options.Pitch.Value > PitchMax)
                throw new ArgumentOutOfRangeException($"Pitch must be >= {PitchMin} and <= {PitchMin}");
        }

        if (semaphore == null)
            semaphore = new SemaphoreSlim(1, 1);

        try
        {
            await semaphore.WaitAsync(cancelToken);
            await PlatformSpeakAsync(text, options, cancelToken);
        }
        finally
        {
            if (semaphore.CurrentCount == 0)
                semaphore.Release();
        }
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalTextToSpeechImplementation : ITextToSpeech
{
    Task<IEnumerable<Locale>> PlatformGetLocalesAsync() =>
        Task.FromResult(SpeechSynthesizer.AllVoices.Select(v =>
        {
            var country = CountryResolver.Resolve(v.Language);
            return (Locale)EssentialsAccessors.CreateLocale(v.Language, country ?? string.Empty, v.DisplayName, v.Id);
        }));

    async Task PlatformSpeakAsync(string text, SpeechOptions? options, CancellationToken cancelToken = default)
    {
        var tcsUtterance = new TaskCompletionSource<bool>();

        try
        {
            var player = new global::Windows.Media.Playback.MediaPlayer();

            var ssml = GetSpeakParametersSSMLProsody(text, options);

            var speechSynthesizer = new SpeechSynthesizer();

            if (!string.IsNullOrWhiteSpace(options?.Locale?.Id))
            {
                var voiceInfo = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == options.Locale.Id) ?? SpeechSynthesizer.DefaultVoice;
                speechSynthesizer.Voice = voiceInfo;
            }

            var stream = await speechSynthesizer.SynthesizeSsmlToStreamAsync(ssml);

            player.MediaEnded += PlayerMediaEnded;
            player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            player.Play();

            void OnCancel()
            {
                if (player != null && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393, 0))
                    player.PlaybackSession.PlaybackRate = 0;
                tcsUtterance.TrySetResult(true);
            }

            using (cancelToken.Register(OnCancel))
            {
                await tcsUtterance.Task;
            }

            player.MediaEnded -= PlayerMediaEnded;
            player.Dispose();

            void PlayerMediaEnded(global::Windows.Media.Playback.MediaPlayer sender, object args)
            {
                tcsUtterance.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Unable to playback stream: " + ex);
            tcsUtterance.TrySetException(ex);
        }
    }

    static string GetSpeakParametersSSMLProsody(string text, SpeechOptions? options)
    {
        var volume = "default";
        var pitch = "default";
        var rate = "default";

        // Look for the specified language, otherwise the default voice
        var locale = options?.Locale?.Language ?? SpeechSynthesizer.DefaultVoice.Language;

        if (options?.Volume.HasValue ?? false)
            volume = (options.Volume.Value * 100f).ToString(CultureInfo.InvariantCulture);

        if (options?.Pitch.HasValue ?? false)
            pitch = ProsodyPitch(options.Pitch);

        // SSML generation
        var ssml = new StringBuilder();
        ssml.AppendLine($"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{locale}'>");
        ssml.AppendLine($"<prosody pitch='{pitch}' rate='{rate}' volume='{volume}'>{text}</prosody> ");
        ssml.AppendLine($"</speak>");

        return ssml.ToString();
    }

    static string ProsodyPitch(float? pitch)
    {
        if (!pitch.HasValue)
            return "default";

        if (pitch.Value <= 0.25f)
            return "x-low";
        else if (pitch.Value > 0.25f && pitch.Value <= 0.75f)
            return "low";
        else if (pitch.Value > 0.75f && pitch.Value <= 1.25f)
            return "medium";
        else if (pitch.Value > 1.25f && pitch.Value <= 1.75f)
            return "high";
        else if (pitch.Value > 1.75f)
            return "x-high";

        return "default";
    }
}