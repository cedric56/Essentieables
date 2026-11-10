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

   The project that receives the Avalonia sources must also reference the packages they use (a
   package cannot add them for you: NuGet ignores package targets while restoring). The build stops
   with **ESS0001** and lists the missing ones:

   ```xml
   <ItemGroup>
     <PackageReference Include="Avalonia.Controls.WebView" Version="12.1.1" />
     <!-- not on net10.0-windows*, which already has the WinRT projection -->
     <PackageReference Include="WindowsContracts.Net.Foundation.UniversalApiContract" Version="5.19041.10" PrivateAssets="compile"
                       Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) != 'windows'" />
     <PackageReference Include="Microsoft.Maui.Platforms.Linux.Gtk4.Essentials" Version="0.1.0-preview.12.26421.1" />
     <!-- net10.0-macos only -->
     <PackageReference Include="Microsoft.Maui.Platforms.MacOS.Essentials" Version="0.1.0-preview.12.26368.2"
                       ExcludeAssets="analyzers"
                       Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'macos'" />
   </ItemGroup>
   ```

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

**Blazor WebAssembly** (one user per process, the services are singletons):

```csharp
using Essentieables;

builder.Services.UseBlazorEssentieables();

var host = builder.Build();
await BlazorEssentials.InitializeAsync(host.Services);   // imports ./BlazorEssentials.js
await host.RunAsync();
```

**Blazor Server** (projects using the Web SDK are compiled with `ESSENTIEABLES_BLAZOR_SERVER`): every
circuit has its own services, JS module and directories, and nothing is shared between users.

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.UseBlazorEssentieables();   // scoped services + a CircuitHandler
```

```razor
@* In an interactive root component (Routes.razor, MainLayout.razor…) *@
<BlazorEssentialsInitializer />
```

`BlazorEssentialsInitializer` calls `BlazorEssentials.InitializeAsync` with the circuit's services after the
first interactive render (JS interop is not available while prerendering). You can call
`InitializeAsync(serviceProvider)` yourself instead, from a component, with the injected `IServiceProvider`;
passing `app.Services` throws, since the services of a circuit only exist in its scope. Repeated calls for
the same circuit do nothing.

On Blazor Server, the static facades (`Preferences.Default`, `Geolocation.Default`…) forward every call to
the services of the circuit that is running. They work in components, event handlers, JS callbacks and the
tasks they start; outside of a circuit (a hosted service, a timer created at startup) they throw
`InvalidOperationException`: inject the services there instead. Prerendering is not a circuit either: use
the facades from `OnAfterRenderAsync` or event handlers, not while the page is prerendered. A Web SDK project that only hosts a
WebAssembly client can opt out with `<EssentieablesBlazorServer>false</EssentieablesBlazorServer>`.

`InitializeAsync` must complete before any Essentials API is used. Before it, the asynchronous APIs and the
synchronous commands (`Vibration.Vibrate`, `PhoneDialer.Open`, `SemanticScreenReader.Announce`,
`HapticFeedback.Perform`) throw `InvalidOperationException`, while the synchronous state (`Preferences.Get`,
`Connectivity.NetworkAccess`, `Clipboard.HasText`…) reads its default value (`BlazorPreferences.IsLoaded`
tells whether the stored preferences are loaded). Failures (permission denied, unsupported API, disconnected circuit)
are reported as exceptions (`PermissionException`, `FeatureNotSupportedException`…) rather than as empty
results; a dismissed file picker or camera popup returns `null`.

### Files

- `FileSystem.CacheDirectory` / `AppDataDirectory`: in WebAssembly, the in-memory file system of the tab;
  on Blazor Server, directories private to the circuit under the server's temp folder, **deleted when the
  circuit ends**. Neither is durable storage.
- `FilePicker`: the files are streamed into the cache directory (`BlazorFilePicker.MaxFileSize`, 100 MB by
  default); browser-supplied names are reduced to a safe file name. `PickOptions.FileTypes` takes an entry for
  `DevicePlatform.Create("Browser")` with extensions and/or MIME types (`".pdf"`, `"image/*"`); without one,
  the extensions and MIME types given for the other platforms are used.
- Captured photos and videos are written to the cache directory too, so `FullPath` is a real file. Read
  picked or captured files with `OpenReadAsync()` on the result (or `Extensions.OpenReadAsync`), or with
  `System.IO`; `FileResult.OpenReadAsync()` called through the base type is not implemented by the portable
  MAUI build.
- `Launcher` only opens `http`, `https`, `mailto`, `tel` and `sms` URIs, in `CanOpenAsync` and `OpenAsync`
  alike. HTML, SVG, XML and script files given to `Launcher.OpenAsync(OpenFileRequest)` are downloaded rather
  than opened, because a `blob:` URL runs with the origin of the app.

### Web authenticator

`BlazorEssentials.UseWebAuthenticator()` returns the route and the HTML of the page that closes the
authentication popup. Map it in the server app:

```csharp
var (pattern, html) = BlazorEssentials.UseWebAuthenticator();
app.MapGet(pattern, () => Results.Content(html, "text/html"));
```

The popup opens `/auth/start?returnUrl=…&authenticationUrl=…&callbackUrl=…`. **This endpoint is
yours to provide**: it starts the sign-in flow and finally redirects to `returnUrl`. Its security is your
responsibility:

- treat every query parameter as untrusted: only redirect to a `returnUrl` that is a relative path of your
  own app (the popup page is `/auth/popup-complete`), and only to identity providers and callback URLs from
  your configuration, never to the values received as-is (open redirect);
- use the OAuth `state` parameter (or the correlation cookie of the ASP.NET Core authentication handlers)
  and PKCE, and validate them on the callback;
- handle errors and cancellation from the provider by still redirecting to `returnUrl`, so the popup closes.

The completion page posts its URL to the opener only on the same origin, and the opener accepts only
messages from that origin and from the popup it opened.

---

## .NET MAUI

The platform implementations already exist in a MAUI app. To expose them through dependency injection:

```csharp
builder.Services.RegisterEssentieables();
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
2. AES-GCM in `localStorage` with a non-extractable key stored in IndexedDB. This protects the values at
   rest only: any script running on the same origin (an XSS, a compromised dependency) can use the key to
   decrypt them, so it is not equivalent to a platform keychain. Clearing site data deletes the key: the
   values then read as `null`. Read/write failures throw; `Remove`/`RemoveAll` are queued (their failures
   are reported by `BlazorSecureStorage.FlushAsync()`).

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

- **Blazor Preferences:** writes are persisted to `localStorage` in the background; a tab closed right
  after a `Set` can lose it. `BlazorPreferences.FlushAsync()` waits for the pending writes and reports their
  failures. As in MAUI, only `string`, `bool`, `int`, `long`, `float`, `double`, `DateTime` and
  `DateTimeOffset` are supported; other types throw `NotSupportedException`.
- **Blazor file dialog:** a dialog closed without a choice is detected with the `cancel` event of the
  input (Chrome 113+, Firefox 91+, Safari 16.4+). On older browsers, `PickAsync` does not return until a file
  is chosen.
- **Blazor Server static facades** use `DispatchProxy` (runtime code generation), which is fine on the
  server JIT; they are not compiled into WebAssembly apps.
- **Linux:** maui-labs' Clipboard and DeviceDisplay use GTK, which is not initialized in an Avalonia
  app: the clipboard does nothing and the display info is a 1920×1080 default. `libgtk-4` is a native
  dependency of that package.
- **Native AOT on Windows:** the share sheet and camera capture use `[ComImport]` interop, which Native AOT does not support.
- **Geocoding** calls the public Nominatim instance by default: respect its
  [usage policy](https://operations.osmfoundation.org/policies/nominatim/). Requests to it are spaced by
  `NominatimGeocoding.MinimumInterval` (one second, process-wide), time out after
  `NominatimGeocoding.Timeout` and 429/503 answers are retried after their `Retry-After` delay. Browsers send
  their own User-Agent: in a WebAssembly app, use your own instance or a server-side proxy.

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
| `tests/Essentieables.Tests` | The MAUI facade accessors still match MAUI's internals; macOS type names; Nominatim parsing, culture-invariant URLs and retries; registration; resolvers. It compiles the Blazor sources as a Blazor Server host and checks the isolation between circuits (services, facades, directories, a circuit never reached from outside of it), the initialization, Preferences and SecureStorage errors, file names and filters, and the schemes the Launcher refuses. |
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
