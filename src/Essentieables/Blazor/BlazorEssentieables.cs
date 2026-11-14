﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Entry point of the browser implementations, shared by Blazor and Avalonia.Browser.
/// <para>
/// <b>Avalonia.Browser</b> (<c>ESSENTIEABLES_AVALONIA_BROWSER</c>): <c>UseEssentieables()</c> registers these implementations
/// and starts <c>InitializeAsync()</c>; the JS interop goes through <c>AvaloniaBrowserJSRuntime</c>.
/// One user per process, as in Blazor WebAssembly.
/// </para>
/// <para>
/// <b>Blazor WebAssembly</b>: one user per process. The services are singletons and the static
/// MAUI facades point at them directly.
/// </para>
/// <para>
/// <b>Blazor Server</b> (<c>ESSENTIEABLES_BLAZOR_SERVER</c>, defined by the package for projects using
/// the Web SDK): every circuit gets its own <see cref="BlazorEssentialsSession"/> (JS module, services,
/// temporary directories). The services are scoped, and the static facades forward each call to the
/// services of the circuit that is currently running (an <see cref="AsyncLocal{T}"/> set by a
/// <c>CircuitHandler</c> for every inbound circuit activity). Nothing is shared between users.
/// </para>
/// </summary>
internal static class BlazorEssentials
{
    static readonly AsyncLocal<BlazorEssentialsSession?> s_current = new();

    // Blazor WebAssembly only: the process belongs to a single user.
    static BlazorEssentialsSession? s_global;

    // A property rather than a const, so the branches that depend on it do not raise CS0162.
    internal static bool IsServer { get; } =
#if ESSENTIEABLES_BLAZOR_SERVER
        true;
#else
        false;
#endif

    /// <summary>The session of the current circuit (Server) or of the process (WebAssembly).</summary>
    internal static BlazorEssentialsSession? CurrentSession
    {
        get => s_current.Value ?? (IsServer ? null : s_global);
        set => s_current.Value = value;
    }

    public static void UseBlazorEssentieables(this IServiceCollection services)
    {
#if ESSENTIEABLES_BLAZOR_SERVER
        AddServerServices(services);
#else
        AddWebAssemblyServices(services);
#endif
    }

#if !ESSENTIEABLES_BLAZOR_SERVER
    static void AddWebAssemblyServices(IServiceCollection services)
    {
        var appInfo = new BlazorAppInfo();
        var preferences = new BlazorPreferences();
        var browser = new BlazorBrowser();
        // Singleton: a WebAssembly process serves one user, and InitializeAsync resolves the
        // services from the root provider (a scoped registration would throw there when scope
        // validation is on, which is the case in Development).
        services.SetDefaults(
            new BlazorAccelerometer(),
            new BlazorAppActions(),
            appInfo,
            new BlazorBarometer(),
            new BlazorBattery(),
            browser,
            new BlazorClipboard(),
            new BlazorCompass(),
            new BlazorConnectivity(),
            new BlazorContacts(),
            new BlazorDeviceDisplay(),
            new BlazorDeviceInfo(),
            new BlazorEmail(),
            new BlazorFilePicker(),
            new BlazorFileSystem(),
            new BlazorFlashlight(),
            new BlazorGeocoding(),
            new BlazorGeolocation(),
            new BlazorGyroscope(),
            new BlazorHapticFeedback(),
            new BlazorLauncher(),
            new BlazorMagnetometer(),
            new BlazorMap(browser),
            new BlazorMediaPicker(),
            new BlazorOrientationSensor(),
            new BlazorPhoneDialer(),
            preferences,
            new BlazorScreenshot(),
            () => new BlazorSecureStorage(),
            new BlazorSemanticScreenReader(),
            new BlazorShare(),
            new BlazorSms(),
            new BlazorTextToSpeech(),
            new BlazorVibration(),
            new BlazorWebAuthenticator(),
            () => new BlazorVersionTracking(preferences, appInfo),
            ServiceLifetime.Singleton);
    }

#if ESSENTIEABLES_AVALONIA_BROWSER
    static Task? s_avaloniaInitialization;

    /// <summary>
    /// Avalonia.Browser: registers the browser implementations (the screenshot comes from Avalonia, which renders the visual tree),
    /// installs them behind the MAUI facades and starts <see cref="InitializeAsync()"/>. Called by <c>UseEssentieables()</c>.
    /// </summary>
    internal static void UseAvaloniaBrowser(IServiceCollection services, IScreenshot screenshot)
    {
        var appInfo = new BlazorAppInfo();
        // Preferences can be read before the JS module is imported (App.OnFrameworkInitializationCompleted…):
        // [JSImport] is synchronous, so the stored values are loaded on first use.
        var preferences = new BlazorPreferences(AvaloniaBrowserStorage.LoadPreferences);
        var browser = new BlazorBrowser();
        var secureStorage = new BlazorSecureStorage();
        var accelerometer = new BlazorAccelerometer();
        var battery = new BlazorBattery();
        var compass = new BlazorCompass();
        var connectivity = new BlazorConnectivity();
        var deviceDisplay = new BlazorDeviceDisplay();
        var deviceInfo = new BlazorDeviceInfo();
        var geolocation = new BlazorGeolocation();
        var gyroscope = new BlazorGyroscope();
        var hapticFeedback = new BlazorHapticFeedback();
        var mediaPicker = new BlazorMediaPicker();
        var orientationSensor = new BlazorOrientationSensor();
        var share = new BlazorShare();
        var vibration = new BlazorVibration();

        services.SetDefaults(
            accelerometer,
            new BlazorAppActions(),
            appInfo,
            new BlazorBarometer(),
            battery,
            browser,
            new BlazorClipboard(),
            compass,
            connectivity,
            new BlazorContacts(),
            deviceDisplay,
            deviceInfo,
            new BlazorEmail(),
            new BlazorFilePicker(),
            new BlazorFileSystem(),
            new BlazorFlashlight(),
            new BlazorGeocoding(),
            geolocation,
            gyroscope,
            hapticFeedback,
            new BlazorLauncher(),
            new BlazorMagnetometer(),
            new BlazorMap(browser),
            mediaPicker,
            orientationSensor,
            new BlazorPhoneDialer(),
            preferences,
            screenshot,
            () => secureStorage,
            new BlazorSemanticScreenReader(),
            share,
            new BlazorSms(),
            new BlazorTextToSpeech(),
            vibration,
            new BlazorWebAuthenticator(),
            () => new BlazorVersionTracking(preferences, appInfo),
            ServiceLifetime.Singleton);

        services.TryAddSingleton<IJSRuntime>(AvaloniaBrowserJSRuntime.Instance);

        // The services InitializeAsync initializes, without requiring a built container.
        var provider = s_avaloniaProvider = new InstanceServiceProvider(new()
        {
            [typeof(IJSRuntime)] = AvaloniaBrowserJSRuntime.Instance,
            [typeof(IAccelerometer)] = accelerometer,
            [typeof(IAppInfo)] = appInfo,
            [typeof(IBattery)] = battery,
            [typeof(ICompass)] = compass,
            [typeof(IConnectivity)] = connectivity,
            [typeof(IDeviceDisplay)] = deviceDisplay,
            [typeof(IDeviceInfo)] = deviceInfo,
            [typeof(IGeolocation)] = geolocation,
            [typeof(IGyroscope)] = gyroscope,
            [typeof(IHapticFeedback)] = hapticFeedback,
            [typeof(IMediaPicker)] = mediaPicker,
            [typeof(IOrientationSensor)] = orientationSensor,
            [typeof(IPreferences)] = preferences,
            [typeof(ISecureStorage)] = secureStorage,
            [typeof(IShare)] = share,
            [typeof(IVibration)] = vibration,
        });

        var initialization = InitializeAsync(provider);
        _ = initialization.ContinueWith(
            static t => Console.Error.WriteLine($"Essentieables: browser initialization failed: {t.Exception?.GetBaseException()}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        s_avaloniaInitialization = initialization;
    }

    /// <summary>
    /// Avalonia.Browser: completes when the JS modules are imported and the services that cache browser state (preferences,
    /// connectivity, device info, persistent file system…) are initialized. <c>UseEssentieables()</c> starts it: await it
    /// before using the asynchronous Essentials APIs (it replaces <c>Platform.Initialize()</c> of Avae.Browser.Essentials).
    /// </summary>
    public static Task InitializeAsync()
    {
        var initialization = s_avaloniaInitialization;
        if (initialization is null || s_avaloniaProvider is null)
            throw new InvalidOperationException("Call UseEssentieables() before BlazorEssentials.InitializeAsync().");
        // A failed attempt (EssentieablesInterop.js not served yet, network error…) is retried.
        if (initialization.IsFaulted || initialization.IsCanceled)
            s_avaloniaInitialization = initialization = InitializeAsync(s_avaloniaProvider);
        return initialization;
    }

    sealed class InstanceServiceProvider(System.Collections.Generic.Dictionary<Type, object> services) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => services.TryGetValue(serviceType, out var service) ? service : null;
    }

    static InstanceServiceProvider? s_avaloniaProvider;
#endif

#else
    static void AddServerServices(IServiceCollection services)
    {
        services.TryAddScoped<BlazorEssentialsSession>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, BlazorEssentialsCircuitHandler>());

        // One instance per circuit (the DI scope of a Blazor Server circuit). Registrations made
        // by the app before this call are kept.
        services.TryAddScoped<IAccelerometer>(_ => new BlazorAccelerometer());
        services.TryAddScoped<IAppActions>(_ => new BlazorAppActions());
        services.TryAddScoped<IAppInfo>(_ => new BlazorAppInfo());
        services.TryAddScoped<IBarometer>(_ => new BlazorBarometer());
        services.TryAddScoped<IBattery>(_ => new BlazorBattery());
        services.TryAddScoped<IBrowser>(_ => new BlazorBrowser());
        services.TryAddScoped<IClipboard>(_ => new BlazorClipboard());
        services.TryAddScoped<ICompass>(_ => new BlazorCompass());
        services.TryAddScoped<IConnectivity>(_ => new BlazorConnectivity());
        services.TryAddScoped<IContacts>(_ => new BlazorContacts());
        services.TryAddScoped<IDeviceDisplay>(_ => new BlazorDeviceDisplay());
        services.TryAddScoped<IDeviceInfo>(_ => new BlazorDeviceInfo());
        services.TryAddScoped<IEmail>(_ => new BlazorEmail());
        services.TryAddScoped<IFilePicker>(_ => new BlazorFilePicker());
        services.TryAddScoped<IFileSystem>(_ => new BlazorFileSystem());
        services.TryAddScoped<IFlashlight>(_ => new BlazorFlashlight());
        services.TryAddScoped<IGeocoding>(_ => new BlazorGeocoding());
        services.TryAddScoped<IGeolocation>(_ => new BlazorGeolocation());
        services.TryAddScoped<IGyroscope>(_ => new BlazorGyroscope());
        services.TryAddScoped<IHapticFeedback>(_ => new BlazorHapticFeedback());
        services.TryAddScoped<ILauncher>(_ => new BlazorLauncher());
        services.TryAddScoped<IMagnetometer>(_ => new BlazorMagnetometer());
        services.TryAddScoped<IMap>(sp => new BlazorMap(sp.GetService<IBrowser>() as BlazorBrowser ?? new BlazorBrowser()));
        services.TryAddScoped<IMediaPicker>(_ => new BlazorMediaPicker());
        services.TryAddScoped<IOrientationSensor>(_ => new BlazorOrientationSensor());
        services.TryAddScoped<IPhoneDialer>(_ => new BlazorPhoneDialer());
        services.TryAddScoped<IPreferences>(_ => new BlazorPreferences());
        services.TryAddScoped<IScreenshot>(_ => new BlazorScreenshot());
        services.TryAddScoped<ISecureStorage>(_ => new BlazorSecureStorage());
        services.TryAddScoped<ISemanticScreenReader>(_ => new BlazorSemanticScreenReader());
        services.TryAddScoped<IShare>(_ => new BlazorShare());
        services.TryAddScoped<ISms>(_ => new BlazorSms());
        services.TryAddScoped<ITextToSpeech>(_ => new BlazorTextToSpeech());
        services.TryAddScoped<IVibration>(_ => new BlazorVibration());
        services.TryAddScoped<IWebAuthenticator>(_ => new BlazorWebAuthenticator());
        services.TryAddScoped<IVersionTracking>(sp => new BlazorVersionTracking(
            sp.GetRequiredService<IPreferences>(), sp.GetRequiredService<IAppInfo>()));

        // The static facades are process-wide: they must never hold a user's instance.
        // They forward to the services of the current circuit instead.
        // Other hosts of the process (Routeables.SignalR…) register their own resolver next to this one.
        EssentialsFacades.AddSessionResolver("Blazor Server", () => CurrentSession?.Services);
        InstallFacades();
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Blazor Server runs on the JIT; this path is not compiled into WebAssembly apps.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Blazor Server apps are not trimmed.")]
    static void InstallFacades()
    {
        // Extended first or last does not matter: a plain facade never replaces an extended one. The extended interfaces keep the
        // Essentieables helpers able to recognize the Blazor implementations behind Share.Default and MediaPicker.Default.
        EssentialsFacades.Install<IAvaeShare, IShare>();
        EssentialsFacades.Install<IAvaeMediaPicker, IMediaPicker>();
        EssentialsFacades.InstallForwarding();
    }
#endif

    /// <summary>Resolves <typeparamref name="T"/> from the services of the current circuit.</summary>
    internal static T GetSessionService<T>() where T : class
    {
        var session = CurrentSession ?? throw new InvalidOperationException(
            $"{typeof(T).Name} was used outside of a Blazor circuit. In Blazor Server the static Essentials " +
            "facades are bound to the circuit that is running; from background work started outside a circuit, " +
            "inject the service into a component and pass it along instead.");
        return session.Services.GetRequiredService<T>();
    }

    public static (string pattern, string html) UseWebAuthenticator()
    {
        const string html = """
        <!doctype html>
        <html>
        <head><meta charset="utf-8"></head>
        <body>
          <p>Authentication completed. You may close this window.</p>
          <script>
            if (window.opener) {
              window.opener.postMessage(
                { type: "web-authenticator-complete", url: window.location.href },
                window.location.origin
              );
              window.close();
            }
          </script>
        </body>
        </html>
        """;

        return ("/auth/popup-complete", html);
    }

    public static bool IsInitialized =>
        CurrentSession?.Module is not null;

    /// <summary>
    /// Throws when the module has not been imported yet. Used by synchronous API surfaces
    /// that cannot await initialization themselves.
    /// </summary>
    internal static void EnsureInitialized() => _ = Module;

    static InvalidOperationException NotInitialized() => new(
#if ESSENTIEABLES_AVALONIA_BROWSER
        "Essentieables is not initialized yet. Await 'BlazorEssentials.InitializeAsync()' (started by UseEssentieables()) " +
        "before using the asynchronous Essentials APIs, and check that wwwroot/EssentieablesInterop.js and BlazorEssentials.js are served.");
#else
        IsServer
            ? "Blazor Essentials is not initialized for this circuit. Call " +
              "'await BlazorEssentials.InitializeAsync(serviceProvider)' from an interactive component " +
              "(OnAfterRenderAsync, firstRender), with the IServiceProvider injected into that component."
            : "Blazor Essentials is not initialized. Call " +
              "'await BlazorEssentials.InitializeAsync(serviceProvider)' during app startup " +
              "before using Essentials APIs.");
#endif

    /// <summary>
    /// Imports <c>./BlazorEssentials.js</c> and initializes the services that cache browser state.
    /// WebAssembly: call once with the host's services. Server: call from an interactive component with
    /// the circuit's <see cref="IServiceProvider"/> (inject it); each circuit is initialized separately.
    /// Calling it again for the same session does nothing.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        BlazorEssentialsSession session;
        if (IsServer)
        {
            // Throws in Development when the root provider is passed (scope validation): the services
            // of a circuit can only come from the circuit's own scope.
            session = provider.GetRequiredService<BlazorEssentialsSession>();
        }
        else
        {
            session = s_global ??= new BlazorEssentialsSession(provider);
        }

        // Makes the session current for the rest of this flow, even when no circuit activity set it.
        CurrentSession = session;

        await session.InitializeAsync(async () =>
        {
#if ESSENTIEABLES_AVALONIA_BROWSER
            // Avalonia.Browser has no Blazor dispatcher: attach the one of EssentieablesInterop.js first. Failures propagate,
            // so the task returned by InitializeAsync() reports them (missing wwwroot/EssentieablesInterop.js…).
            await AvaloniaBrowserJSRuntime.AttachAsync().ConfigureAwait(false);
#endif
            var module = await ImportModuleAsync(provider.GetRequiredService<IJSRuntime>()).ConfigureAwait(false);
#if ESSENTIEABLES_AVALONIA_BROWSER
            // No prerendering nor circuit here: a missing module is an error worth reporting through the task.
            if (module is null)
                throw new InvalidOperationException("BlazorEssentials.js could not be imported: check that it is served next to index.html (wwwroot/).");
#else
            if (module is null)
                return false;
#endif
            session.Module = module;
            if (!IsServer)
                await BlazorFileSystem.MountPersistentStorageAsync(module).ConfigureAwait(false);
            await InitializeServicesAsync(session.Services).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    static async Task InitializeServicesAsync(IServiceProvider provider)
    {
        // Pattern matches instead of casts: an app may have registered its own implementation.
        if (provider.GetService<IConnectivity>() is BlazorConnectivity connectivity)
            await connectivity.InitializeAsync();
        if (provider.GetService<IAppInfo>() is BlazorAppInfo appInfo)
            await appInfo.InitializeAsync();
        if (provider.GetService<IHapticFeedback>() is BlazorHapticFeedback feedback)
            await feedback.InitializeAsync();
        if (provider.GetService<IPreferences>() is BlazorPreferences preferences)
            await preferences.InitializeAsync();
        if (provider.GetService<ISecureStorage>() is BlazorSecureStorage storage)
            await storage.InitializeAsync();
        if (provider.GetService<IBattery>() is BlazorBattery battery)
            await battery.InitializeAsync();
        if (provider.GetService<IDeviceInfo>() is BlazorDeviceInfo info)
            await info.InitializeAsync();
        if (provider.GetService<IVibration>() is BlazorVibration vibration)
            await vibration.InitializeAsync();
        if (provider.GetService<IDeviceDisplay>() is BlazorDeviceDisplay display)
            await display.InitializeAsync();
        if (provider.GetService<IAccelerometer>() is BlazorAccelerometer accelerometer)
            await accelerometer.InitializeAsync();
        if (provider.GetService<IGyroscope>() is BlazorGyroscope gyroscope)
            await gyroscope.InitializeAsync();
        if (provider.GetService<ICompass>() is BlazorCompass compass)
            await compass.InitializeAsync();
        if (provider.GetService<IOrientationSensor>() is BlazorOrientationSensor orientation)
            await orientation.InitializeAsync();
        if (provider.GetService<IShare>() is BlazorShare share)
            await share.InitializeAsync();
        if (provider.GetService<IMediaPicker>() is BlazorMediaPicker mediaPicker)
            await mediaPicker.InitializeAsync();
        if (provider.GetService<IGeolocation>() is BlazorGeolocation geolocation)
            await geolocation.InitializeAsync();
    }

    internal static async Task<IJSObjectReference?> ImportModuleAsync(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            return await js.InvokeAsync<IJSObjectReference>("import", "./BlazorEssentials.js")
                           .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Prerendering (no JS runtime yet), disconnected circuit or missing wwwroot/BlazorEssentials.js.
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }

    /// <summary>The JS module of the current session; throws when it was not imported.</summary>
    internal static IJSObjectReference Module =>
        CurrentSession?.Module ?? throw NotInitialized();
}

/// <summary>
/// Everything that belongs to one user: the JS module, the DI scope holding the services and the
/// temporary directories. Server: one per circuit (scoped). WebAssembly: one per process.
/// </summary>
internal sealed class BlazorEssentialsSession : IAsyncDisposable
{
    readonly SemaphoreSlim _initGate = new(1, 1);
    bool _initialized;
    int _disposed;
    string? _rootDirectory;

    public BlazorEssentialsSession(IServiceProvider services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public IServiceProvider Services { get; }

    public IJSObjectReference? Module { get; internal set; }

    internal async Task InitializeAsync(Func<Task<bool>> initialize)
    {
        await _initGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized || Volatile.Read(ref _disposed) != 0)
                return;
            _initialized = await initialize().ConfigureAwait(false);
        }
        finally
        {
            _initGate.Release();
        }
    }

    /// <summary>
    /// A directory private to this session. Server: under the temp folder, keyed by the session id, and
    /// deleted when the circuit ends. WebAssembly: <c>/_cache</c> and <c>/_appdata</c> in the file system of the browser tab,
    /// kept in IndexedDB when the app links IDBFS (see <see cref="BlazorFileSystem.IsPersistent"/>).
    /// </summary>
    internal string GetDirectory(string name)
    {
        var root = _rootDirectory ??= BlazorEssentials.IsServer
            ? Path.Combine(Path.GetTempPath(), "essentieables", Id)
            : "/";
        var path = Path.Combine(root, BlazorEssentials.IsServer ? name : BlazorFileSystem.GetBrowserDirectoryName(name));
        Directory.CreateDirectory(path);
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var module = Module;
        Module = null;
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
            {
                // The circuit is already gone.
            }
        }

        if (BlazorEssentials.IsServer && _rootDirectory is { } root)
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}

#if ESSENTIEABLES_BLAZOR_SERVER
/// <summary>Makes the circuit's session current for every inbound activity of the circuit.</summary>
internal sealed class BlazorEssentialsCircuitHandler(BlazorEssentialsSession session)
    : Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler
{
    // Run before the app's handlers, so they can use the Essentials facades.
    public override int Order => int.MinValue;

    public override Func<Microsoft.AspNetCore.Components.Server.Circuits.CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<Microsoft.AspNetCore.Components.Server.Circuits.CircuitInboundActivityContext, Task> next)
        => async context =>
        {
            // Set inside an async method: the value flows into next() and everything it starts,
            // and is not leaked back to the caller.
            BlazorEssentials.CurrentSession = session;
            await next(context);
        };

    public override Task OnCircuitClosedAsync(
        Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        => session.DisposeAsync().AsTask();
}
#endif
