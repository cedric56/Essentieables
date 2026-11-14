# Essentieables

[![CI](https://github.com/cedric56/Essentieables/actions/workflows/ci.yml/badge.svg)](https://github.com/cedric56/Essentieables/actions/workflows/ci.yml)

**.NET MAUI Essentials for Avalonia and Blazor.** Your ViewModels keep using the regular
`Microsoft.Maui.Essentials` contracts (`IGeolocation`, `IPreferences`, `IFilePicker`, …) and static
facades (`Geolocation.Default`, `Preferences.Default`, …), and run unchanged on:

- **Avalonia** desktop (Windows, Linux, macOS) and browser (Avalonia.Browser runs the same browser implementations as Blazor)
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

The package contains a small compiled library (registration, shared helpers such as the Nominatim
geocoder) plus **source files compiled into the project that references it**. It depends on
[Essentieables.Facades](src/Essentieables.Facades/README.md), which holds the MAUI facade accessors and the
per-session forwarding facades and injects nothing into your build.
`src/Essentieables/build/Essentieables.targets` detects the host, so there is nothing to declare:

| Host | Detected when the project… | Sources compiled into it |
|------|----------------------------|--------------------------|
| Avalonia | references Avalonia (directly, transitively or through a project reference) | `Avalonia/*.avalonia.cs` and the Avalonia.Controls.Maui.Essentials sources; built for the browser, `Blazor/*.cs` too, and `BlazorEssentials.js`, `EssentieablesInterop.js` and `dotnet-js-interop.js` are copied to `wwwroot/` |
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
| [Avalonia.Controls.Maui.Essentials](https://github.com/AvaloniaUI/Avalonia.Controls.Maui) | File and media picking, file system, preferences, screenshot, web authenticator on Avalonia desktop (the screenshot in the browser too) |
| [Microsoft.Maui.Platforms.Linux.Gtk4.Essentials](https://github.com/dotnet/maui-labs) | Linux services |
| [Microsoft.Maui.Platforms.MacOS.Essentials](https://github.com/dotnet/maui-labs) | macOS services (`net10.0-macos` only) |
| Ports of the MAUI Windows (WinRT) implementations | Windows services, including on plain `net10.0` where MAUI only ships stubs |
| `BlazorEssentials.js` and `Blazor/*.cs` | Browser APIs, for Blazor **and Avalonia.Browser**: one implementation, run through the Blazor JS runtime or through `AvaloniaBrowserJSRuntime` |
| Ports of Avae.Windows.Essentials, Avae.macOS.Essentials and Avae.Browser.Essentials | Jump list activation, MAUI storage locations and loopback sign-in on Windows; camera, contacts and `ASWebAuthenticationSession` on macOS; persistent files, `<meta>` app info and detailed device info in the browser |

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

   The project that receives the Avalonia sources also needs the packages they use. Reference
   **Essentieables.Avalonia** there instead of Essentieables: it has no code, and brings Essentieables and
   those packages as dependencies that NuGet chooses for each target framework of the app:

   ```xml
   <ItemGroup>
     <PackageReference Include="Essentieables.Avalonia" Version="1.0.0-preview.1" />
   </ItemGroup>
   ```

   | Target framework | Packages besides Essentieables and Avalonia.Controls.WebView |
   |------------------|---------------------------------------------------------------|
   | `net10.0` (desktop, any OS) | `WindowsContracts.Net.Foundation.UniversalApiContract`, `Microsoft.Maui.Platforms.Linux.Gtk4.Essentials` |
   | `net10.0-windows10.0.19041` | — (the SDK projects WinRT) |
   | `net10.0-macos` | `Microsoft.Maui.Platforms.MacOS.Essentials` |
   | `net10.0-android`, `net10.0-ios`, `net10.0-browser` | — |

   Only the code a target framework can run is compiled: the Windows ports on `net10.0` and
   `net10.0-windows*` (`ESSENTIEABLES_WINDOWS`), the GTK4 branch on `net10.0` (`ESSENTIEABLES_LINUX`), the
   macOS one on `net10.0-macos`. An Android, iOS or browser head therefore gets no Windows or Linux package.
   In a multi-platform app, reference it in each head that calls `UseEssentieables()`, not in a shared
   `net10.0` project, or the heads inherit the desktop packages of that project.

   A package cannot add references from its targets (NuGet ignores package targets while restoring), so a
   project that receives the Avalonia sources without Essentieables.Avalonia stops with **ESS0001**, which
   lists the missing packages for its target framework.

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

Services you registered before calling `UseEssentieables` are kept.

MAUI's `MainThread` is static and cannot be replaced: in an Avalonia app use `AvaeMainThread` (same methods, on the
Avalonia UI thread). For permissions, the MAUI generic helpers work with the Essentieables permission classes, for
instance `await Permissions.RequestAsync<AvaePermissions.LocationWhenInUse>()` on Windows.

### Avalonia.Browser

An Avalonia app built for the browser runs **the Blazor implementations** (`Blazor/*.cs` and `BlazorEssentials.js`):
there is one browser implementation of each service, shared with Blazor WebAssembly and Blazor Server. Blazor provides
its own JS interop runtime; Avalonia.Browser has none, so the package brings `AvaloniaBrowserJSRuntime`, an `IJSRuntime`
built on `[JSImport]` and on the dispatcher of `@microsoft/dotnet-js-interop` (`EssentieablesInterop.js`). It is also
registered in the services, so the app can inject `IJSRuntime` for its own scripts.

1. Reference **Essentieables.Avalonia** from the browser head (it brings `Microsoft.JSInterop` for `net10.0-browser`). The
   head needs a `wwwroot/` folder (the Avalonia template has one) and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`
   (`[JSImport]`). `BlazorEssentials.js`, `EssentieablesInterop.js` and `dotnet-js-interop.js` are copied to `wwwroot/` on build.
2. Call `UseEssentieables()` as on the desktop. It registers the browser services and starts their initialization (the
   JS modules are imported, the cached state is loaded). Await it before using the asynchronous APIs; it replaces
   `Platform.Initialize()` of Avae.Browser.Essentials:

   ```csharp
   private static async Task Main(string[] args)
   {
       await BuildAvaloniaApp()
           .WithInterFont()
           .UseEssentieables()
           .StartBrowserAppAsync("out");

       await BlazorEssentials.InitializeAsync();
   }
   ```

   `Preferences` can be read before that: in the browser `[JSImport]` is synchronous, so the stored values are loaded on
   first use. The preferences of the former browser implementation (Avalonia.Controls.Maui.Essentials) are moved once.
3. Optional, in `index.html` (read by `AppInfo`, as Avae.Browser.Essentials did; otherwise the page title, the host name
   and the assembly version are used):

   ```html
   <meta name="name" content="My app" />
   <meta name="package" content="com.example.myapp" />
   <meta name="app-version" content="1.2.3" />
   <meta name="build-version" content="42" />
   ```

- **Persistent files.** `FileSystem.AppDataDirectory` (`/_appdata`) and `CacheDirectory` (`/_cache`) are kept in IndexedDB
  when the app links Emscripten's IDBFS: add `<EmccExtraLDFlags>-lidbfs.js</EmccExtraLDFlags>` (requires the `wasm-tools`
  workload). They are the stores of Avae.Browser.Essentials, so its files are found again. Writes are persisted
  automatically, and when the tab is hidden; `BlazorFileSystem.PersistAsync()`, `StartPeriodicFlushAsync()` and
  `StopPeriodicFlushAsync()` replace `PersistFsAsync`, `StartPeriodicFlush` and `StopPeriodicFlush`;
  `BlazorFileSystem.IsPersistent` tells whether IDBFS is there. This applies to Blazor WebAssembly too.
- **Camera.** `MediaPicker.CapturePhotoAsync`/`CaptureVideoAsync` open the camera popup of the Blazor implementation
  (HTTPS required).
- **Device info.** Phones, tablets (iPad and iPad Pro included) and desktops are told apart; `Model` and `Manufacturer` are
  the device's when the browser exposes them (Android, iPhone/iPad from the screen and GPU). `BlazorDeviceInfo` adds
  `RealPlatform`, `OperatingSystem`, `OperatingSystemVersion`, `Architecture`, `GpuRenderer`, `GpuVendor`, `IsTablet`, `IsIPad`…
- **Web authenticator.** Without a server, the popup opens `WebAuthenticatorOptions.Url` itself and completes when the
  provider redirects it to `CallbackUrl`, which must be on the origin of the app (any page: the popup location is read once it
  is back on that origin; the page of `BlazorEssentials.UseWebAuthenticator()` and a `BroadcastChannel("token")` post, as with
  Avae.Browser.Essentials, work too). Set `BlazorWebAuthenticator.UseStartEndpoint = true` to use the `/auth/start` flow of
  Blazor instead.
- **JSInterop.** `JSInterop.Fetch`, `Eval`, `Query` and `Open` of Avae.Browser.Essentials are available (namespace `Essentieables`).
- The screenshot comes from Avalonia, which renders its own visual tree.
- Trimmed apps: the JS interop serializes with reflection, so the package sets `JsonSerializerIsReflectionEnabledByDefault`
  to `true` in browser projects that do not set it.

### Windows

- **Jump list.** `AppActions.Current.AppActionActivated` is raised for the action the app was launched with, once its first
  window is open. Single instance is opt-in: start `Main` with
  `if (AvaeAppActionsActivation.TryForwardToRunningInstance()) return;` to hand the action to a running instance of the same
  executable (WM_COPYDATA) instead of starting a second one (not under `dotnet app.dll`). `AppAction.Icon`, when the MAUI build
  has it, is a file of the app package (`ms-appx:///`, `.png` added when there is no extension).
- **Storage locations.** As in MAUI and Avae.Windows.Essentials: `FileSystem.AppDataDirectory` is
  `%LOCALAPPDATA%\<Publisher>\<Package>\Data` (`…\Cache` for the cache, the `ApplicationData` folders when packaged),
  `Preferences` and `SecureStorage` are in the `Settings` folder next to it, and `Preferences` uses
  `ApplicationData.LocalSettings` in a packaged (MSIX) app. Values written by the preview builds of Essentieables
  (`%APPDATA%\<Assembly>\Settings`) are read when those files do not exist yet. `OpenAppPackageFileAsync` reads the file
  next to the executable, then the Avalonia resource (`avares://`).
- **Sign-in.** A `http://localhost:port/…` callback opens the sign-in page in the system browser and receives the callback
  with an `HttpListener` (`AvaeWebAuthenticator.Timeout`, 5 minutes by default), as Avae.Windows.Essentials did: Google and
  other providers refuse embedded web views. Other callbacks still use the Avalonia broker. The same applies on Linux and macOS.
- **Screenshot.** `Screenshot.Default.CaptureAsync(window)` and `CaptureAsync(visual)` capture a given window or control
  (every Avalonia host).
- `EssentieablesPlatform.OnLaunched(action)`, `OnActivated(topLevel)` and `OnPlatformWindowInitialized(window)` replace the
  `Platform` helpers of Avae.Windows.Essentials; `Platform.MapServiceToken` is MAUI's own on `net10.0-windows*`.

### macOS (`net10.0-macos`)

- **Camera.** `MediaPicker.CapturePhotoAsync`/`CaptureVideoAsync` open a camera window (AVFoundation) attached to the active
  window; JPEG photos and QuickTime videos are written to the cache directory. Add `NSCameraUsageDescription` (and
  `NSMicrophoneUsageDescription` for the sound of videos) to `Info.plist`, plus the camera/audio entitlements when sandboxed.
- **Contacts.** `Contacts.Default.GetAllAsync()` reads the address book (`CNContactStore`); `NSContactsUsageDescription` is
  required. `PickContactAsync` is not supported on macOS.
- **Sign-in.** `ASWebAuthenticationSession` for custom-scheme callbacks (no `Info.plist` entry needed), the system browser and
  an `HttpListener` for `http://localhost` callbacks.

### Migrating from Avae.Windows.Essentials, Avae.macOS.Essentials, Avae.Browser.Essentials

Those packages were full copies of MAUI Essentials; Essentieables keeps the real `Microsoft.Maui.Essentials` package and
only replaces the implementations behind `Xxx.Default`.

1. Remove the old package and `<UseMauiEssentials>`; reference Essentieables.Avalonia.
2. Replace `.UseMauiEssentials()` with `.UseEssentieables()` (or `services.UseEssentieables()`).
3. Browser: remove `essentials.js` and the `dotnetRuntime` export of `main.js`; replace `await Platform.Initialize()` with
   `await BlazorEssentials.InitializeAsync()`. Keep `-lidbfs.js` to find the files again.
4. Static APIs that are not facades of MAUI: `MainThread` → `AvaeMainThread`, `Platform.OnLaunched/OnActivated` →
   `EssentieablesPlatform`, `Permissions.Xxx` → `AvaePermissions.Xxx` (Windows), `JSInterop` → `Essentieables.JSInterop` (browser).
5. Files given by the pickers may not be backed by a path that MAUI can open: read them with `OpenReadAsync()` from
   `Essentieables.Extensions`.

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
`InvalidOperationException`: inject the services there instead. The forwarding comes from
[Essentieables.Facades](src/Essentieables.Facades/README.md), so another multi-user host of the same process
(a Routeables.SignalR server, say) can share the facades: each call goes to the session that is running, whichever host owns it. Prerendering is not a circuit either: use
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

- `FileSystem.CacheDirectory` / `AppDataDirectory`: in WebAssembly, `/_cache` and `/_appdata` in the file system of the
  tab, kept in IndexedDB when the app links IDBFS (`<EmccExtraLDFlags>-lidbfs.js</EmccExtraLDFlags>`, see
  [Avalonia.Browser](#avaloniabrowser)), in memory otherwise; on Blazor Server, directories private to the circuit under the
  server's temp folder, **deleted when the circuit ends** (not durable storage).
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

| Service | Windows | Linux | macOS (`net10.0-macos`) | Other OS | Blazor and Avalonia browser |
|---------|---------|-------|-------------------------|----------|-----------------------------|
| Accelerometer, Barometer, Compass, Gyroscope, Magnetometer, OrientationSensor | WinRT | maui-labs | — | — | JS |
| AppActions | WinRT jump list, with activation | maui-labs | — | — | JS |
| AppInfo | Essentieables | maui-labs | maui-labs | — | JS (`<meta>` tags) |
| Battery, Connectivity | WinRT | maui-labs | maui-labs | — | JS |
| DeviceInfo | WinRT | maui-labs | maui-labs | — | JS (phone/tablet/desktop, model) |
| Browser, Launcher, Map | WinRT | maui-labs | maui-labs | — | JS |
| Clipboard | WinRT | maui-labs (GTK, see limitations) | maui-labs | — | JS |
| Contacts | WinRT | maui-labs stub | Essentieables (CNContactStore) | — | JS |
| DeviceDisplay | Essentieables | maui-labs (GTK, see limitations) | maui-labs | — | JS |
| Email | Essentieables | maui-labs | maui-labs | — | JS |
| FilePicker | Avalonia | Avalonia | Avalonia | Avalonia | JS |
| FileSystem | MAUI locations³ | Avalonia | Avalonia | Avalonia | JS (IndexedDB with IDBFS) |
| Preferences | MAUI locations³ / LocalSettings | Avalonia | Avalonia | Avalonia | JS (`localStorage`) |
| Screenshot | Avalonia | Avalonia | Avalonia | Avalonia | Avalonia in Avalonia browser, not supported in Blazor |
| WebAuthenticator | system browser⁴ / Avalonia | system browser⁴ / Avalonia | system browser⁴ / ASWebAuthenticationSession | Avalonia | JS (popup) |
| MediaPicker | Avalonia + Windows Camera app | Avalonia | Avalonia + camera window | Avalonia | JS (camera popup) |
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
"Other OS" is an Avalonia app on an OS without its own column (Android, iOS through Avalonia's own heads).
A plain `net10.0` app running on macOS is in the "other OS" column; target `net10.0-macos` to get the
maui-labs services. "Stub" means `IsSupported` is `false` or the call throws `FeatureNotSupportedException`.
What the JS implementations can do depends on the browser (sensors, vibration, contacts, torch…).

1. On `net10.0-windows…` the MAUI implementation is used instead, and it needs a map service token.
2. AES-GCM in `localStorage` with a non-extractable key stored in IndexedDB. This protects the values at
   rest only: any script running on the same origin (an XSS, a compromised dependency) can use the key to
   decrypt them, so it is not equivalent to a platform keychain. Clearing site data deletes the key: the
   values then read as `null`. Read/write failures throw; `Remove`/`RemoveAll` are queued (their failures
   are reported by `BlazorSecureStorage.FlushAsync()`).
3. `%LOCALAPPDATA%\<Publisher>\<Package>` (`Data`, `Cache`, `Settings`), the `ApplicationData` folders in a packaged app.
4. For `http://localhost:port/` callbacks (`HttpListener`); other callbacks use the second implementation.

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
- **Fire-and-forget APIs in the browser** (`Vibrate`, `HapticFeedback.Perform`, `Announce`, `PhoneDialer.Open`, sensors'
  `Start`) validate their arguments and support synchronously, so `FeatureNotSupportedException` & co reach the caller as
  with MAUI; a failure of the browser afterwards is written to the console. A sensor the browser refuses to start (iOS
  motion permission denied…) stops monitoring and raises `BlazorSensorBase.StartFailed`.
- **macOS**: `NSApplication.CheckForIllegalCrossThreadCalls` is no longer turned off; call the Essentials APIs from the UI
  thread (`AvaeMainThread`).
- **Avalonia.Browser** shares the browser limitations above (Preferences persisted in the background, file dialog
  cancellation, what each browser supports). `IJSStreamReference` streams must be read asynchronously (`CopyToAsync`):
  the browser has a single thread.
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
dotnet pack -c Release -o artifacts/packages -p:Version=1.0.0-local   # Essentieables and Essentieables.Facades

# Consumer checks against that package (see tests/Consumers)
dotnet run --project tests/Consumers/AvaloniaSmoke
ConsumerTargetFrameworks="net10.0;net10.0-android" dotnet build tests/Consumers/LibraryConsumer
```

Building every target framework needs the `android`, `ios` and `macos` workloads (`ios` and `macos` on a Mac).

The repository holds two packages, each in its own project:

| Project | Package |
|---------|---------|
| [`src/Essentieables`](src/Essentieables) | `Essentieables`: registration, helpers, and the host sources injected into the app (`Avalonia/`, `Blazor/`, `build/Essentieables.targets`) |
| [`src/Essentieables.Avalonia`](src/Essentieables.Avalonia) | `Essentieables.Avalonia`: no code; Essentieables and the packages of its Avalonia sources, for each target framework (nuspec) |
| [`src/Essentieables.Facades`](src/Essentieables.Facades) | `Essentieables.Facades`: the MAUI facade accessors and the per-session forwarding facades; no injected sources. `Essentieables` depends on it |

| Project | What it checks |
|---------|----------------|
| `tests/Essentieables.Tests` | The MAUI facade accessors still match MAUI's internals; macOS type names; Nominatim parsing, culture-invariant URLs and retries; registration; resolvers; the forwarding facades shared by multi-user hosts (resolver chain, extended facades kept, restored after a replacement); no Windows or GTK4 package or code for Android, iOS, macOS and the browser. It compiles the Blazor sources as a Blazor Server host and checks the isolation between circuits (services, facades, directories, a circuit never reached from outside of it), the initialization, Preferences and SecureStorage errors, file names and filters, and the schemes the Launcher refuses. The browser implementations shared with Avalonia.Browser: device info (tablets, models, real platform), `<meta>` app info, the IndexedDB directory names, preferences loaded synchronously, and the browser assets and JSInterop dependency. |
| `tests/Consumers/LibraryConsumer` | The injected Avalonia sources compile for each consumer target framework, with Essentieables.Avalonia as their only package reference (so Android gets no Windows or GTK4 package) |
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
