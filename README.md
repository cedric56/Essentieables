# Essentieables

Cross-host implementations of **.NET MAUI Essentials**-style APIs so the same ViewModels can run on **Avalonia**, **Blazor / WebAssembly**, and platform backends (Windows, Linux, macOS, mobile TFMs).

Contracts stay aligned with `Microsoft.Maui.Essentials` (`IGeolocation`, `IClipboard`, `IVibration`, …). Interfaces are registered through DI and availables with static accessors (`Geolocation.Default`, `Clipboard.Default`, `Vibration.Default`, …).

**License:** MIT — [LICENSE.txt](LICENSE.txt)

---

**TFMs:** `net11.0` (Windows/Linux supported), `net11.0-browser`, `net11.0-ios`, `net11.0-android`, `net11.0-macos`, `net11.0-windows10.0.19041`

### Enable Avalonia feature pack

```xml
<PropertyGroup>
  <UseAvae>true</UseAvae>
</PropertyGroup>
```

Blazor sources are included automatically when the Web / Blazor WebAssembly SDK is used.

**Host apps** that call `UseAvaeEssentials()` must restore **Avalonia** and **Avalonia.Controls.Maui.Essentials** at runtime (the library may use `PrivateAssets=compile` so pure ViewModel projects are not forced to take Avalonia).

---

## Quick start

### Avalonia

```csharp
services.UseAvaeEssentials();
```
OR

```chsarp
 public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseAvaeEssentials()
            .UsePlatformDetect();
```

### Blazor

```csharp
services.UseAvaeBlazorEssentials();
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
