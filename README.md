# Essentieables

[![CI](https://github.com/cedric56/Essentieables/actions/workflows/ci.yml/badge.svg)](https://github.com/cedric56/Essentieables/actions/workflows/ci.yml)

**.NET MAUI Essentials for Avalonia and Blazor.** Your ViewModels keep using the regular
`Microsoft.Maui.Essentials` contracts (`IGeolocation`, `IPreferences`, `IFilePicker`, …) and static
facades (`Geolocation.Default`, `Preferences.Default`, …), and run unchanged on:

- **Avalonia** desktop (Windows, Linux, macOS) and browser
- **Blazor** WebAssembly and Blazor Server
- **.NET MAUI** itself (its platform implementations are left in place)

Essentieables does not redefine any contract. It plugs implementations into the MAUI facades and
registers the same instances in `IServiceCollection`, so `Geolocation.Default` and an injected
`IGeolocation` are the same object.

---

## Contents

- [How it works](#how-it-works)
- [Installation](#installation)
- [Avalonia](#avalonia)
- [Blazor](#blazor)
- [.NET MAUI](#net-maui)
- [Which implementation runs where](#which-implementation-runs-where)
- [Helpers](#helpers)
- [Known limitations](#known-limitations)
- [Building and testing](#building-and-testing)
- [License](#license)

---

## How it works

The package contains a small compiled library (registration, MAUI facade accessors, shared helpers
such as the Nominatim geocoder) plus **source files compiled into the project that references it**.
`build/Essentieables.targets` detects the host, so there is nothing to declare:

| Host | Detected when the project… | Sources compiled into it |
|------|----------------------------|--------------------------|
| Avalonia | references Avalonia (directly, transitively or through a project reference) | `Avalonia/*.avalonia.cs` and the Avalonia.Controls.Maui.Essentials sources |
| Blazor WebAssembly, Blazor Server | uses `Microsoft.NET.Sdk.Web` or `Microsoft.NET.Sdk.BlazorWebAssembly` | `Blazor/*.cs`; `BlazorEssentials.js` is copied to `wwwroot/` |

The sources are added at build time: they are compiled and visible to IntelliSense, but do not
appear in Solution Explorer. The detection also applies when the package comes through a project
reference (App → ViewModels → Essentieables): the ViewModels library, which references neither
Avalonia nor ASP.NET Core, gets nothing, and the app gets the sources.
To override the detection, set `<EssentieablesHost>` to `Avalonia`, `Blazor` or `None`.

Platform code is selected by your project's own `#if` symbols (`WINDOWS`, `MACOS`) and by runtime
`OperatingSystem` checks, so one package serves every target framework.

Existing implementations are reused rather than rewritten:

| Source | Used for |
|--------|----------|
| [Avalonia.Controls.Maui.Essentials](https://github.com/AvaloniaUI/Avalonia.Controls.Maui) | File and media picking, file system, preferences, screenshot, web authenticator, on every Avalonia host |
| [Microsoft.Maui.Platforms.Linux.Gtk4.Essentials](https://github.com/dotnet/maui-labs) | Linux services |
| [Microsoft.Maui.Platforms.MacOS.Essentials](https://github.com/dotnet/maui-labs) | macOS services (`net10.0-macos` only) |
| Ports of the MAUI Windows (WinRT) implementations | Windows services, including on plain `net10.0` where MAUI only ships stubs |
| `BlazorEssentials.js` | Browser APIs for Blazor |

> Avalonia.Controls.Maui.Essentials is compiled from source because its package only targets `net11.0`.
> **Do not also reference the `Avalonia.Controls.Maui.Essentials` package**, or its types are defined twice (CS0436).

---

## Installation

```xml
<ItemGroup>
  <PackageReference Include="Essentieables" Version="1.0.0-preview.1" />
</ItemGroup>
```

**Target frameworks:** `net10.0`, `net10.0-browser`, `net10.0-android`, `net10.0-ios`,
`net10.0-macos`, `net10.0-windows10.0.19041`.

---

## Avalonia

1. Reference the package from the app, directly or through a project it references (typically the
   ViewModels library). Because the app references Avalonia, the Avalonia sources are compiled into it.

   ViewModel libraries that do not reference Avalonia only get the compiled library: they use the MAUI
   contracts. Every project that references Avalonia receives the sources. They are `internal`, so a
   multi-platform app (shared `App` project + Desktop/Android/iOS heads) still compiles when both get
   them, with CS0436 warnings for the Avalonia.Controls.Maui.Essentials types. To compile them once,
   set `<EssentieablesHost>None</EssentieablesHost>` in the projects that do not call `UseEssentieables()`.

   The extra packages the Avalonia sources need (Avalonia.Controls.WebView, the WinRT contracts,
   maui-labs Linux/macOS) are added automatically to projects that reference an `Avalonia*` package
   directly: you do not need to list them.

2. Register the services on the UI thread, once Avalonia is set up (the UI dispatcher is captured):

   ```csharp
   using Essentieables;

   services.UseEssentieables();   // installs the static facades and registers them in DI
   ```

   Without dependency injection, the `AppBuilder` overload installs the static facades only:

   ```csharp
   public static AppBuilder BuildAvaloniaApp()
       => AppBuilder.Configure<App>()
           .UsePlatformDetect()
           .UseEssentieables();
   ```

Services you registered before calling `UseEssentieables` are kept. An
`Microsoft.Maui.Dispatching.IDispatcher` backed by the Avalonia dispatcher is registered too.

---

## Blazor

The Blazor sources are compiled into projects using the Web or Blazor WebAssembly SDK, and
`BlazorEssentials.js` is copied to the project's `wwwroot/` folder on build (the folder must exist).
A Razor class library is not detected: set `<EssentieablesHost>Blazor</EssentieablesHost>` there.

```csharp
using Essentieables;

builder.Services.UseBlazorEssentieables();

var app = builder.Build();
await BlazorEssentials.InitializeAsync(app.Services);   // imports ./BlazorEssentials.js
```

`InitializeAsync` must complete before any Essentials API is used; synchronous APIs throw
`InvalidOperationException` otherwise.

### Web authenticator

`BlazorEssentials.UseWebAuthenticator()` returns the route and the HTML of the page that closes the
authentication popup. Map it in the server app:

```csharp
var (pattern, html) = BlazorEssentials.UseWebAuthenticator();
app.MapGet(pattern, () => Results.Content(html, "text/html"));
```

The popup opens `/auth/start?returnUrl=…&authenticationUrl=…&callbackUrl=…`. **This endpoint is
yours to provide**: it starts the sign-in flow and finally redirects to `returnUrl`.

---

## .NET MAUI

The platform implementations already exist in a MAUI app. To expose them through dependency injection:

```csharp
builder.Services.RegisterAvaeEssentials();
```

---

## Which implementation runs where

| Service | Windows | Linux | macOS (`net10.0-macos`) | Avalonia browser, other OS | Blazor |
|---------|---------|-------|-------------------------|----------------------------|--------|
| Accelerometer, Barometer, Compass, Gyroscope, Magnetometer, OrientationSensor | WinRT | maui-labs | — | — | JS |
| AppActions | WinRT jump list | maui-labs | — | — | JS |
| AppInfo | Essentieables | maui-labs | maui-labs | — | JS |
| Battery, Connectivity, DeviceInfo | WinRT | maui-labs | maui-labs | — | JS |
| Browser, Launcher, Map | WinRT | maui-labs | maui-labs | — | JS |
| Clipboard | WinRT | maui-labs (GTK, see limitations) | maui-labs | — | JS |
| Contacts | WinRT | maui-labs stub | — | — | JS |
| DeviceDisplay | Essentieables | maui-labs (GTK, see limitations) | maui-labs | — | JS |
| Email | Essentieables | maui-labs | maui-labs | — | JS |
| FilePicker, FileSystem, Preferences, Screenshot, WebAuthenticator | Avalonia | Avalonia | Avalonia | Avalonia | JS |
| MediaPicker | Avalonia + Windows Camera app | Avalonia | Avalonia | Avalonia | JS |
| Flashlight | WinRT | maui-labs stub | maui-labs stub | — | JS |
| Geocoding | Nominatim¹ | Nominatim | Nominatim | — | Nominatim |
| Geolocation | Essentieables | maui-labs | maui-labs | — | JS |
| HapticFeedback | WinRT | Avalonia (no-op) | maui-labs | Avalonia (no-op) | JS |
| PhoneDialer, Sms | Essentieables / WinRT | maui-labs | maui-labs | — | JS |
| SecureStorage | WinRT (DPAPI) | maui-labs | maui-labs | — | JS (WebCrypto)² |
| SemanticScreenReader | Essentieables | maui-labs | maui-labs | — | JS |
| Share | WinRT share sheet | maui-labs | maui-labs | — | JS |
| TextToSpeech | WinRT | maui-labs | maui-labs | — | JS |
| Vibration | not supported | maui-labs stub | maui-labs | — | JS |
| VersionTracking | MAUI | MAUI | MAUI | — | Essentieables |

— : nothing is installed and the MAUI facade throws `NotImplementedInReferenceAssemblyException`.
A plain `net10.0` app running on macOS is in the "other OS" column; target `net10.0-macos` to get the
maui-labs services. "Stub" means `IsSupported` is `false` or the call throws `FeatureNotSupportedException`.
What the JS implementations can do depends on the browser (sensors, vibration, contacts, torch…).

1. On `net10.0-windows…` the MAUI implementation is used instead, and it needs a map service token.
2. AES-GCM in `localStorage` with a non-extractable key stored in IndexedDB: any script on the same origin can decrypt it.

---

## Helpers

`Essentieables.Extensions` adds helpers that understand Avalonia file results (which are not always
backed by a path on disk) and fall back to the standard MAUI behaviour otherwise:

```csharp
using var stream = await fileResult.OpenReadAsync();              // any FileBase
await Email.Default.ComposeAsync(files, new EmailMessage { … });   // attach FileBase files
await Share.Default.RequestAsync("Title", files);                  // share FileBase files
```

`NominatimGeocoding` can be pointed at your own Nominatim instance (`new NominatimGeocoding(httpClient, "https://geo.example.org/")`).
`ContentTypeResolver.Resolve("photo.jpg")` and `CountryResolver.Resolve("fr-FR")` are public as well.

---

## Known limitations

- **Blazor Server:** the JS module and the services are process-wide singletons. With several
  circuits, every user shares the module of the last circuit that called `InitializeAsync`
  (preferences, clipboard, geolocation of another browser). Only use Blazor Server with a single user
  until per-circuit services are implemented.
- **Linux:** maui-labs' Clipboard and DeviceDisplay use GTK, which is not initialized in an Avalonia
  app: the clipboard does nothing and the display info is a 1920×1080 default. `libgtk-4` is a native
  dependency of that package.
- **Native AOT on Windows:** the share sheet and camera capture use `[ComImport]` interop, which Native AOT does not support.
- **Geocoding** calls the public Nominatim instance by default: respect its
  [usage policy](https://operations.osmfoundation.org/policies/nominatim/) (about one request per second).

---

## Building and testing

```bash
# Unit tests (net10.0 only, no workload required)
dotnet test tests/Essentieables.Tests -p:EssentieablesTargetFrameworks=net10.0

# Package for a subset of target frameworks.
# Lists go in environment variables: `-p:` splits its value on ';' and ',' (MSB1006).
export EssentieablesTargetFrameworks="net10.0;net10.0-android"   # PowerShell: $env:EssentieablesTargetFrameworks = "..."
dotnet pack -c Release -o artifacts/packages -p:Version=1.0.0-local

# Consumer checks against that package (see tests/Consumers)
dotnet run --project tests/Consumers/AvaloniaSmoke
ConsumerTargetFrameworks="net10.0;net10.0-android" dotnet build tests/Consumers/LibraryConsumer
```

Building every target framework needs the `android`, `ios` and `macos` workloads (`ios` and `macos` on a Mac).

| Project | What it checks |
|---------|----------------|
| `tests/Essentieables.Tests` | The MAUI facade accessors still match MAUI's internals; macOS type names; Nominatim parsing and culture-invariant URLs; registration; resolvers. It also compiles the Blazor sources. |
| `tests/Consumers/LibraryConsumer` | The injected Avalonia sources compile for each consumer target framework |
| `tests/Consumers/AvaloniaSmoke` | `UseEssentieables()` runs in a headless Avalonia app and the core services work |
| `tests/Consumers/BlazorWasmConsumer`, `BlazorServerConsumer` | The Blazor sources compile in both hosts and `BlazorEssentials.js` is copied |

[CI](.github/workflows/ci.yml) runs all of it on **Linux** (with Android, browser and Blazor),
**Windows** (with `net10.0-windows10.0.19041`) and **macOS** (with iOS and `net10.0-macos`).

### Contributing

Issues and pull requests are welcome. Before adding an implementation, check whether
Microsoft.Maui.Essentials, maui-labs or Avalonia.Controls.Maui.Essentials already provides one.

---

## License

Copyright (c) 2026 Cedric Boutin. MIT, see [LICENSE.txt](LICENSE.txt).

Essentieables incorporates code from .NET MAUI, Avalonia.Controls.Maui, MarketAlly and ASP.NET Core,
and geocodes with OpenStreetMap data (© OpenStreetMap contributors, ODbL). Their notices are in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), which ships in the package and must accompany
any redistribution. Apps that show geocoding results must credit OpenStreetMap.

Thanks to the Avalonia and .NET MAUI teams.
