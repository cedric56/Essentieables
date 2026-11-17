

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Text to speech backed by the Web Speech API (speechSynthesis).</summary>
internal class BlazorTextToSpeech : ITextToSpeech
{
    sealed record Voice(string Name, string Lang, bool IsDefault, string? VoiceURI);

    /// <summary>The voices of the browser (speechSynthesis.getVoices()): Name is the voice name, Id its voiceURI.</summary>
    public async Task<IEnumerable<Locale>> GetLocalesAsync()
    {
        var json = await BlazorEssentials.Module.InvokeAsync<string>("speechGetVoices").ConfigureAwait(false);
        var voices = JsonSerializer.Deserialize<Voice[]>(json, s_json) ?? [];
        return voices.Select(v =>
        {
            // "en-US" → language "en", country "US"
            var parts = (v.Lang ?? string.Empty).Replace('_', '-').Split('-', 2);
            return (Locale)EssentialsAccessors.CreateLocale(
                parts[0], parts.Length > 1 ? parts[1] : string.Empty, v.Name, v.VoiceURI ?? v.Name);
        }).ToList();
    }

    static readonly JsonSerializerOptions s_json = new() { PropertyNameCaseInsensitive = true };

    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();

        using var registration = cancelToken.CanBeCanceled
            ? cancelToken.Register(static state => ((BlazorTextToSpeech)state!).RequestSpeechCancellation(), this)
            : default;

        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("speak", text,
                options?.Locale?.Language is { Length: > 0 } language && options.Locale.Country is { Length: > 0 } country
                    ? $"{language}-{country}"
                    : options?.Locale?.Language,
                options?.Pitch ?? -1,
                -1,
                options?.Volume ?? -1,
                options?.Locale?.Id).ConfigureAwait(false);
        }
        catch (JSException ex) when (BlazorEssentials.MapJSException(ex, "Text to speech") is { } mapped)
        {
            // "not-allowed": no user activation yet (autoplay policy).
            throw mapped;
        }
        cancelToken.ThrowIfCancellationRequested();
    }

    private void RequestSpeechCancellation()
    {
        // CancellationToken.Register accepts an Action, so this callback
        // must remain synchronous. The helper observes its own failures.
        _ = CancelSpeechSafelyAsync();
    }

    private static async Task CancelSpeechSafelyAsync()
    {
        try
        {
            await BlazorEssentials.Module
                .InvokeVoidAsync("speechCancel")
                .ConfigureAwait(false);
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif
    }
}
