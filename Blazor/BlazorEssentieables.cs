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
/// Entry point of the Blazor implementations.
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
        EssentialsAccessors.SetAccelerometer(null, BlazorCircuitProxy<IAccelerometer, IAccelerometer>.Create());
        EssentialsAccessors.SetAppActions(null, BlazorCircuitProxy<IAppActions, IAppActions>.Create());
        EssentialsAccessors.SetAppInfo(null, BlazorCircuitProxy<IAppInfo, IAppInfo>.Create());
        EssentialsAccessors.SetBarometer(null, BlazorCircuitProxy<IBarometer, IBarometer>.Create());
        EssentialsAccessors.SetBattery(null, BlazorCircuitProxy<IBattery, IBattery>.Create());
        EssentialsAccessors.SetBrowser(null, BlazorCircuitProxy<IBrowser, IBrowser>.Create());
        EssentialsAccessors.SetClipboard(null, BlazorCircuitProxy<IClipboard, IClipboard>.Create());
        EssentialsAccessors.SetCompass(null, BlazorCircuitProxy<ICompass, ICompass>.Create());
        EssentialsAccessors.SetConnectivity(null, BlazorCircuitProxy<IConnectivity, IConnectivity>.Create());
        EssentialsAccessors.SetContacts(null, BlazorCircuitProxy<IContacts, IContacts>.Create());
        EssentialsAccessors.SetDeviceDisplay(null, BlazorCircuitProxy<IDeviceDisplay, IDeviceDisplay>.Create());
        EssentialsAccessors.SetDeviceInfo(null, BlazorCircuitProxy<IDeviceInfo, IDeviceInfo>.Create());
        EssentialsAccessors.SetEmail(null, BlazorCircuitProxy<IEmail, IEmail>.Create());
        EssentialsAccessors.SetFilePicker(null, BlazorCircuitProxy<IFilePicker, IFilePicker>.Create());
        EssentialsAccessors.SetFileSystem(null, BlazorCircuitProxy<IFileSystem, IFileSystem>.Create());
        EssentialsAccessors.SetFlashlight(null, BlazorCircuitProxy<IFlashlight, IFlashlight>.Create());
        EssentialsAccessors.SetGeocoding(null, BlazorCircuitProxy<IGeocoding, IGeocoding>.Create());
        EssentialsAccessors.SetGeolocation(null, BlazorCircuitProxy<IGeolocation, IGeolocation>.Create());
        EssentialsAccessors.SetGyroscope(null, BlazorCircuitProxy<IGyroscope, IGyroscope>.Create());
        EssentialsAccessors.SetHapticFeedback(null, BlazorCircuitProxy<IHapticFeedback, IHapticFeedback>.Create());
        EssentialsAccessors.SetLauncher(null, BlazorCircuitProxy<ILauncher, ILauncher>.Create());
        EssentialsAccessors.SetMagnetometer(null, BlazorCircuitProxy<IMagnetometer, IMagnetometer>.Create());
        EssentialsAccessors.SetMap(null, BlazorCircuitProxy<IMap, IMap>.Create());
        EssentialsAccessors.SetMediaPicker(null, BlazorCircuitProxy<IAvaeMediaPicker, IMediaPicker>.Create());
        EssentialsAccessors.SetOrientationSensor(null, BlazorCircuitProxy<IOrientationSensor, IOrientationSensor>.Create());
        EssentialsAccessors.SetPhoneDialer(null, BlazorCircuitProxy<IPhoneDialer, IPhoneDialer>.Create());
        EssentialsAccessors.SetPreferences(null, BlazorCircuitProxy<IPreferences, IPreferences>.Create());
        EssentialsAccessors.SetScreenshot(null, BlazorCircuitProxy<IScreenshot, IScreenshot>.Create());
        EssentialsAccessors.SetSecureStorage(null, BlazorCircuitProxy<ISecureStorage, ISecureStorage>.Create());
        EssentialsAccessors.SetSemanticScreenReader(null, BlazorCircuitProxy<ISemanticScreenReader, ISemanticScreenReader>.Create());
        EssentialsAccessors.SetShare(null, BlazorCircuitProxy<IAvaeShare, IShare>.Create());
        EssentialsAccessors.SetSms(null, BlazorCircuitProxy<ISms, ISms>.Create());
        EssentialsAccessors.SetTextToSpeech(null, BlazorCircuitProxy<ITextToSpeech, ITextToSpeech>.Create());
        EssentialsAccessors.SetVibration(null, BlazorCircuitProxy<IVibration, IVibration>.Create());
        EssentialsAccessors.SetWebAuthenticator(null, BlazorCircuitProxy<IWebAuthenticator, IWebAuthenticator>.Create());
        EssentialsAccessors.SetVersionTracking(null, BlazorCircuitProxy<IVersionTracking, IVersionTracking>.Create());
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
        IsServer
            ? "Blazor Essentials is not initialized for this circuit. Call " +
              "'await BlazorEssentials.InitializeAsync(serviceProvider)' from an interactive component " +
              "(OnAfterRenderAsync, firstRender), with the IServiceProvider injected into that component."
            : "Blazor Essentials is not initialized. Call " +
              "'await BlazorEssentials.InitializeAsync(serviceProvider)' during app startup " +
              "before using Essentials APIs.");

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
            var module = await ImportModuleAsync(provider.GetRequiredService<IJSRuntime>()).ConfigureAwait(false);
            if (module is null)
                return false;
            session.Module = module;
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
    /// deleted when the circuit ends. WebAssembly: the in-memory file system of the browser tab.
    /// </summary>
    internal string GetDirectory(string name)
    {
        var root = _rootDirectory ??= BlazorEssentials.IsServer
            ? Path.Combine(Path.GetTempPath(), "essentieables", Id)
            : "/";
        var path = Path.Combine(root, name);
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

/// <summary>
/// Installed in a static MAUI facade on Blazor Server: forwards every member (methods, properties,
/// events) to the <typeparamref name="TService"/> registered in the current circuit.
/// <typeparamref name="TFacade"/> may extend it (IAvaeShare, IAvaeMediaPicker) so that the
/// Essentieables helpers still recognize the Blazor implementations behind the facade.
/// </summary>
internal class BlazorCircuitProxy<TFacade, TService> : System.Reflection.DispatchProxy
    where TFacade : class, TService
    where TService : class
{
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Blazor Server runs on the JIT; this path is not compiled into WebAssembly apps.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Blazor Server apps are not trimmed.")]
    public static TFacade Create() => Create<TFacade, BlazorCircuitProxy<TFacade, TService>>();

    protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var target = BlazorEssentials.GetSessionService<TService>();
        if (!targetMethod.DeclaringType!.IsInstanceOfType(target))
            throw new NotSupportedException(
                $"{target.GetType().Name} does not implement {targetMethod.DeclaringType.Name}.{targetMethod.Name}.");
        try
        {
            return targetMethod.Invoke(target, args);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
#endif
