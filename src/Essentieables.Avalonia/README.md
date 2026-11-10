# Essentieables.Avalonia

Reference this package, instead of Essentieables, in each Avalonia app project that calls `UseEssentieables()` (the Desktop, Android,
iOS… heads):

```xml
<PackageReference Include="Essentieables.Avalonia" Version="1.0.0-preview.1" />
```

It has no code: it brings [Essentieables](https://github.com/cedric56/Essentieables) and the packages its Avalonia sources use, chosen
by NuGet for each target framework of the app:

| Target framework | Packages besides Essentieables and Avalonia.Controls.WebView |
|------------------|---------------------------------------------------------------|
| `net10.0` (desktop, any OS) | `WindowsContracts.Net.Foundation.UniversalApiContract`, `Microsoft.Maui.Platforms.Linux.Gtk4.Essentials` |
| `net10.0-windows10.0.19041` | — (the SDK projects WinRT) |
| `net10.0-macos` | `Microsoft.Maui.Platforms.MacOS.Essentials` |
| `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-browser` | — |

Only the code a target framework can run is compiled into the app, so an Android, iOS or browser app references nothing of Windows or
Linux.
