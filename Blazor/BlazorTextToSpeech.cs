using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Text to speech backed by the Web Speech API (speechSynthesis).</summary>
internal class BlazorTextToSpeech : ITextToSpeech
{
    //internal static async Task<string> SpeechGetVoicesAsync(IJSRuntime js) =>
    //    await ModuleRef(js).InvokeAsync<string>("speechGetVoices");


    public async Task<IEnumerable<Locale>> GetLocalesAsync()
    {
        // Locale has no public constructor, so voices cannot be surfaced through the
        // MAUI Locale type. SpeakAsync honors SpeechOptions.Volume and Pitch; a
        // specific voice can only be selected by the browser's language matching.
        return [];
    }

    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();

        try
        {
            using var registration = cancelToken.CanBeCanceled
                ? cancelToken.Register(static state => ((BlazorTextToSpeech)state!).RequestSpeechCancellation(), this)
                : default;

            await BlazorEssentials.Module.InvokeVoidAsync("speak", text,
                options?.Locale?.Language,
                options?.Pitch ?? -1,
                -1,
                options?.Volume ?? -1).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotSupportedException("The Web Speech API is not available in this browser.");
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
