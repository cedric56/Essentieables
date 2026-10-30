# Essentieables

Cross-host implementations of **.NET MAUI Essentials**-style APIs so the same ViewModels can run on **Avalonia**, **Blazor / WebAssembly**, and platform backends (Windows, Linux, macOS, mobile TFMs).

Contracts stay aligned with `Microsoft.Maui.Essentials` (`IGeolocation`, `IClipboard`, `IVibration`, …). Interfaces are registered through DI and availables with static accessors (`Geolocation.Default`, `Clipboard.Default`, `Vibration.Default`, …).

**License:** MIT — [LICENSE.txt](LICENSE.txt)

---

**TFMs:** `net10.0` (Windows/Linux supported), `net10.0-browser`, `net10.0-ios`, `net10.0-android`, `net10.0-macos`, `net10.0-windows10.0.19041`

### Enable Avalonia feature pack

```xml
<PropertyGroup>
  <UseEssentieables>true</UseEssentieables>
</PropertyGroup>
```

Blazor sources are included automatically when the Web / Blazor WebAssembly SDK is used.

The sources of **Avalonia.Controls.Maui.Essentials** are compiled into the host app: do **not** also reference the `Avalonia.Controls.Maui.Essentials` package, or the types will be defined twice (CS0436).

---

## Quick start

### Avalonia

```csharp
services.UseEssentieables();
```
OR

```csharp
 public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseEssentieables()
            .UsePlatformDetect();
```

### Blazor

```csharp
services.UseBlazorEssentieables();
```

`BlazorEssentials.js` is automatically copied in `wwwroot`, and call `InitializeAsync()`.

```csharp
var app = builder.Build();
await BlazorEssentials.InitializeAsync(app.Services);
```
Or on Server when circuit provider changed
```csharp
async provider =>
    await BlazorEssentials.InitializeAsync(provider));
```

### MAUI-only

```csharp
services.RegisterAvaeEssentials();
```

# Contributing

Contributions are welcome! Please submit issues or pull requests to the GitHub repository. Ensure your code follows the project’s coding standards.

# Acknowledgments

Thanks to the Avalonia team for their robust UI framework.

Gratitude to the MAUI team for providing cross-platform essentials.
