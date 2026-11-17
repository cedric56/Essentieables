

namespace Essentieables;

public class AvaePlatform
{
    /// <summary>
    /// True when a .NET MAUI application is running (Microsoft.Maui.Controls.Application.Current is set). Not decided by the
    /// target framework: an Avalonia app built for net10.0-windows uses the Windows build of this library too.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Absent type: false.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Absent property: false.")]
    public static bool IsMaui
        => System.Type.GetType("Microsoft.Maui.Controls.Application, Microsoft.Maui.Controls", throwOnError: false)
               ?.GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
               ?.GetValue(null) is not null;
}
