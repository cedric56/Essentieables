using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

namespace Essentieables;

/// <summary>
/// Forwarding facades for servers that host many users in one process (Blazor Server, Routeables.SignalR…). The static MAUI facades
/// (<c>Preferences.Default</c>, <c>Geolocation.Default</c>, <c>Connectivity.Current</c>…) are process-wide, so they must never hold the
/// service of a user. Once installed, each one forwards every member to the service of the session that is running, found by asking the
/// session resolvers that the hosts registered with <see cref="AddSessionResolver"/>.
/// </summary>
/// <remarks>
/// Several hosts can share a process: each one registers its resolver, and the first that knows a running session answers. Installing again
/// restores the forwarding facades (after a test or <c>SetDefaults</c> replaced one) and never replaces an extended facade (<see cref="Install{TFacade, TService}"/>) with a plain one.
/// </remarks>
public static class EssentialsFacades
{
    private static readonly object s_gate = new();
    private static readonly Dictionary<Type, (Type Facade, object Instance)> s_installed = [];
    private static volatile Resolver[] s_resolvers = [];

    private static readonly Dictionary<Type, Action<object?>> s_setters = new()
    {
        [typeof(IAccelerometer)] = s => EssentialsAccessors.SetAccelerometer(null, (IAccelerometer?)s),
        [typeof(IAppActions)] = s => EssentialsAccessors.SetAppActions(null, (IAppActions?)s),
        [typeof(IAppInfo)] = s => EssentialsAccessors.SetAppInfo(null, (IAppInfo?)s),
        [typeof(IBarometer)] = s => EssentialsAccessors.SetBarometer(null, (IBarometer?)s),
        [typeof(IBattery)] = s => EssentialsAccessors.SetBattery(null, (IBattery?)s),
        [typeof(IBrowser)] = s => EssentialsAccessors.SetBrowser(null, (IBrowser?)s),
        [typeof(IClipboard)] = s => EssentialsAccessors.SetClipboard(null, (IClipboard?)s),
        [typeof(ICompass)] = s => EssentialsAccessors.SetCompass(null, (ICompass?)s),
        [typeof(IConnectivity)] = s => EssentialsAccessors.SetConnectivity(null, (IConnectivity?)s),
        [typeof(IContacts)] = s => EssentialsAccessors.SetContacts(null, (IContacts?)s),
        [typeof(IDeviceDisplay)] = s => EssentialsAccessors.SetDeviceDisplay(null, (IDeviceDisplay?)s),
        [typeof(IDeviceInfo)] = s => EssentialsAccessors.SetDeviceInfo(null, (IDeviceInfo?)s),
        [typeof(IEmail)] = s => EssentialsAccessors.SetEmail(null, (IEmail?)s),
        [typeof(IFlashlight)] = s => EssentialsAccessors.SetFlashlight(null, (IFlashlight?)s),
        [typeof(IVersionTracking)] = s => EssentialsAccessors.SetVersionTracking(null, (IVersionTracking?)s),
        [typeof(IGeocoding)] = s => EssentialsAccessors.SetGeocoding(null, (IGeocoding?)s),
        [typeof(IGeolocation)] = s => EssentialsAccessors.SetGeolocation(null, (IGeolocation?)s),
        [typeof(IGyroscope)] = s => EssentialsAccessors.SetGyroscope(null, (IGyroscope?)s),
        [typeof(ILauncher)] = s => EssentialsAccessors.SetLauncher(null, (ILauncher?)s),
        [typeof(IMagnetometer)] = s => EssentialsAccessors.SetMagnetometer(null, (IMagnetometer?)s),
        [typeof(IMap)] = s => EssentialsAccessors.SetMap(null, (IMap?)s),
        [typeof(IOrientationSensor)] = s => EssentialsAccessors.SetOrientationSensor(null, (IOrientationSensor?)s),
        [typeof(IPhoneDialer)] = s => EssentialsAccessors.SetPhoneDialer(null, (IPhoneDialer?)s),
        [typeof(ISecureStorage)] = s => EssentialsAccessors.SetSecureStorage(null, (ISecureStorage?)s),
        [typeof(ISemanticScreenReader)] = s => EssentialsAccessors.SetSemanticScreenReader(null, (ISemanticScreenReader?)s),
        [typeof(IShare)] = s => EssentialsAccessors.SetShare(null, (IShare?)s),
        [typeof(ISms)] = s => EssentialsAccessors.SetSms(null, (ISms?)s),
        [typeof(ITextToSpeech)] = s => EssentialsAccessors.SetTextToSpeech(null, (ITextToSpeech?)s),
        [typeof(IVibration)] = s => EssentialsAccessors.SetVibration(null, (IVibration?)s),
        [typeof(IFilePicker)] = s => EssentialsAccessors.SetFilePicker(null, (IFilePicker?)s),
        [typeof(IFileSystem)] = s => EssentialsAccessors.SetFileSystem(null, (IFileSystem?)s),
        [typeof(IHapticFeedback)] = s => EssentialsAccessors.SetHapticFeedback(null, (IHapticFeedback?)s),
        [typeof(IMediaPicker)] = s => EssentialsAccessors.SetMediaPicker(null, (IMediaPicker?)s),
        [typeof(IPreferences)] = s => EssentialsAccessors.SetPreferences(null, (IPreferences?)s),
        [typeof(IScreenshot)] = s => EssentialsAccessors.SetScreenshot(null, (IScreenshot?)s),
        [typeof(IWebAuthenticator)] = s => EssentialsAccessors.SetWebAuthenticator(null, (IWebAuthenticator?)s),
    };

    /// <summary>The 36 Essentials contracts that have a static facade.</summary>
    public static IReadOnlyCollection<Type> Contracts => s_setters.Keys;

    /// <summary>The hosts that registered a session resolver, in the order they are asked.</summary>
    public static IReadOnlyList<string> Hosts => [.. s_resolvers.Select(r => r.Host)];

    /// <summary>
    /// Registers how <paramref name="host"/> finds the services of the session that is running (the Blazor circuit, the Routeables
    /// session…). <paramref name="services"/> returns <see langword="null"/> when none of its sessions runs on the calling flow. Registering
    /// a host again replaces its resolver.
    /// </summary>
    /// <returns>Removes the resolver when disposed.</returns>
    public static IDisposable AddSessionResolver(string host, Func<IServiceProvider?> services)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(services);
        var resolver = new Resolver(host, services);
        lock (s_gate)
            s_resolvers = [.. s_resolvers.Where(r => r.Host != host), resolver];
        return new Registration(resolver);
    }

    /// <summary>The services of the session that is running, or <see langword="null"/> outside of any session.</summary>
    public static IServiceProvider? CurrentServices
    {
        get
        {
            foreach (var resolver in s_resolvers)
            {
                if (resolver.Services() is { } services)
                    return services;
            }
            return null;
        }
    }

    /// <summary>The service <typeparamref name="T"/> of the session that is running; throws outside of any session.</summary>
    /// <exception cref="InvalidOperationException">No session runs on the calling flow, or it has no <typeparamref name="T"/>.</exception>
    public static T Resolve<T>() where T : class
    {
        var services = CurrentServices ?? throw new InvalidOperationException(OutsideMessage(typeof(T)));
        return services.GetRequiredService<T>();
    }

    /// <summary>Whether the facade of <paramref name="contract"/> forwards to the running session, and through which interface.</summary>
    public static bool TryGetInstalled(Type contract, [NotNullWhen(true)] out Type? facade)
    {
        lock (s_gate)
        {
            var found = s_installed.TryGetValue(contract, out var installed);
            facade = found ? installed.Facade : null;
            return found;
        }
    }

    /// <summary>Installs the forwarding facade of every Essentials contract, keeping the extended ones already installed.</summary>
    [RequiresDynamicCode("The forwarding facades are DispatchProxy instances: for servers on the JIT.")]
    [RequiresUnreferencedCode("The forwarding facades call the session services by reflection.")]
    public static void InstallForwarding()
    {
        Install<IAccelerometer, IAccelerometer>();
        Install<IAppActions, IAppActions>();
        Install<IAppInfo, IAppInfo>();
        Install<IBarometer, IBarometer>();
        Install<IBattery, IBattery>();
        Install<IBrowser, IBrowser>();
        Install<IClipboard, IClipboard>();
        Install<ICompass, ICompass>();
        Install<IConnectivity, IConnectivity>();
        Install<IContacts, IContacts>();
        Install<IDeviceDisplay, IDeviceDisplay>();
        Install<IDeviceInfo, IDeviceInfo>();
        Install<IEmail, IEmail>();
        Install<IFlashlight, IFlashlight>();
        Install<IVersionTracking, IVersionTracking>();
        Install<IGeocoding, IGeocoding>();
        Install<IGeolocation, IGeolocation>();
        Install<IGyroscope, IGyroscope>();
        Install<ILauncher, ILauncher>();
        Install<IMagnetometer, IMagnetometer>();
        Install<IMap, IMap>();
        Install<IOrientationSensor, IOrientationSensor>();
        Install<IPhoneDialer, IPhoneDialer>();
        Install<ISecureStorage, ISecureStorage>();
        Install<ISemanticScreenReader, ISemanticScreenReader>();
        Install<IShare, IShare>();
        Install<ISms, ISms>();
        Install<ITextToSpeech, ITextToSpeech>();
        Install<IVibration, IVibration>();
        Install<IFilePicker, IFilePicker>();
        Install<IFileSystem, IFileSystem>();
        Install<IHapticFeedback, IHapticFeedback>();
        Install<IMediaPicker, IMediaPicker>();
        Install<IPreferences, IPreferences>();
        Install<IScreenshot, IScreenshot>();
        Install<IWebAuthenticator, IWebAuthenticator>();
    }

    /// <summary>
    /// Installs the forwarding facade of <typeparamref name="TService"/> as a <typeparamref name="TFacade"/>, an interface that extends it
    /// (<c>IAvaeShare</c> for <c>IShare</c>…) so that helpers can still test the facade for it. A member of <typeparamref name="TFacade"/>
    /// that the service of the session does not implement throws <see cref="NotSupportedException"/>. Kept when a facade of the same or a
    /// more derived interface was installed, which is set again instead.
    /// </summary>
    /// <exception cref="ArgumentException"><typeparamref name="TService"/> is not an Essentials contract.</exception>
    /// <exception cref="InvalidOperationException">Another host already installed an unrelated extended facade for it.</exception>
    [RequiresDynamicCode("The forwarding facades are DispatchProxy instances: for servers on the JIT.")]
    [RequiresUnreferencedCode("The forwarding facades call the session services by reflection.")]
    public static void Install<TFacade, TService>()
        where TFacade : class, TService
        where TService : class
    {
        if (!s_setters.TryGetValue(typeof(TService), out var set))
            throw new ArgumentException($"{typeof(TService).Name} is not an Essentials contract with a static facade.", nameof(TService));
        lock (s_gate)
        {
            if (s_installed.TryGetValue(typeof(TService), out var current))
            {
                if (!typeof(TFacade).IsAssignableFrom(current.Facade) && !current.Facade.IsAssignableFrom(typeof(TFacade)))
                    throw new InvalidOperationException(
                        $"The facade of {typeof(TService).Name} already forwards as {current.Facade.Name}, which {typeof(TFacade).Name} does not extend.");
            }
            // Set again even when it was installed: the facade may have been replaced since (SetDefaults, a test…).
            var installed = current.Instance is not null && typeof(TFacade).IsAssignableFrom(current.Facade)
                ? current
                : (typeof(TFacade), ForwardingFacade<TFacade, TService>.Create());
            set(installed.Instance);
            s_installed[typeof(TService)] = installed;
        }
    }

    private static string OutsideMessage(Type service)
    {
        var hosts = s_resolvers.Select(r => r.Host).ToArray();
        var running = hosts.Length == 0 ? "no host registered a session resolver" : $"no {string.Join(" or ", hosts)} session runs here";
        return $"{service.Name} was used outside of a session: the static Essentials facades forward to the service of the session that is " +
            $"running, and {running}. Code started outside of a session (a hosted service, a timer created at startup, work that lost " +
            "the flow) has no user to ask: inject the service where a session runs and pass it along.";
    }

    private sealed record Resolver(string Host, Func<IServiceProvider?> Services);

    private sealed class Registration(Resolver resolver) : IDisposable
    {
        public void Dispose()
        {
            lock (s_gate)
                s_resolvers = [.. s_resolvers.Where(r => !ReferenceEquals(r, resolver))];
        }
    }
}

/// <summary>
/// Forwards every member (methods, properties, events) of <typeparamref name="TFacade"/> to the <typeparamref name="TService"/> of the
/// session that is running.
/// </summary>
internal class ForwardingFacade<TFacade, TService> : DispatchProxy
    where TFacade : class, TService
    where TService : class
{
    [RequiresDynamicCode("DispatchProxy")]
    [RequiresUnreferencedCode("DispatchProxy")]
    public static TFacade Create() => Create<TFacade, ForwardingFacade<TFacade, TService>>();

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reached only through Create, which carries the requirement.")]
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        var target = EssentialsFacades.Resolve<TService>();
        if (!targetMethod.DeclaringType!.IsInstanceOfType(target))
            throw new NotSupportedException(
                $"{target.GetType().Name} does not implement {targetMethod.DeclaringType.Name}.{targetMethod.Name}.");
        try
        {
            return targetMethod.Invoke(target, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
