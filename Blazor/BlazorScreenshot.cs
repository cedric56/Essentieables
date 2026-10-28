using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using System.Threading.Tasks;

namespace Essentieables;


/// <summary>Screenshots of the page cannot be captured from within the browser sandbox.</summary>
public class BlazorScreenshot : IScreenshot
{
    public bool IsCaptureSupported => false;

    public Task<IScreenshotResult> CaptureAsync() =>
        throw new FeatureNotSupportedException("Screenshots are not supported in the browser.");
}
