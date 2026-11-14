using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Maui.Essentials;
using Avalonia.Media.Imaging;
using Microsoft.Maui.Media;
using System;
using System.Threading.Tasks;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace Essentieables;

/// <summary>
/// Captures of a given window or control, as <c>Screenshot.Default.CaptureAsync(window)</c> and <c>CaptureAsync(visual)</c> of
/// Avae.Windows.Essentials did. <see cref="IScreenshot.CaptureAsync()"/> captures the active window. Every Avalonia host
/// (desktop, mobile, browser) renders its own visual tree, so these work everywhere.
/// </summary>
internal static class AvaeScreenshotExtensions
{
    /// <summary>Captures <paramref name="window"/>.</summary>
    public static Task<IScreenshotResult> CaptureAsync(this IScreenshot screenshot, Window window)
        => CaptureAsync(screenshot, (Visual)window);

    /// <summary>
    /// Captures <paramref name="visual"/> and its children, at the render scaling of its window (so the image has the
    /// resolution of the screen). The visual must be attached to a visual tree and laid out.
    /// </summary>
    public static async Task<IScreenshotResult> CaptureAsync(this IScreenshot screenshot, Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);

        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var scaling = TopLevel.GetTopLevel(visual)?.RenderScaling ?? 1.0;
            var size = visual is TopLevel topLevel ? topLevel.ClientSize : visual.Bounds.Size;
            var pixelWidth = (int)Math.Round(size.Width * scaling);
            var pixelHeight = (int)Math.Round(size.Height * scaling);

            if (pixelWidth <= 0 || pixelHeight <= 0)
                throw new InvalidOperationException("Cannot capture a screenshot of a visual that has no size (not laid out or not visible).");

            var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96 * scaling, 96 * scaling));
            bitmap.Render(visual);

            return (IScreenshotResult)new AvaloniaScreenshotResult(bitmap, pixelWidth, pixelHeight);
        });
    }
}
