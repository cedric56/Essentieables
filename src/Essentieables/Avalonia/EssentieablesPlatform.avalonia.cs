using Avalonia.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading;
using System.Threading.Tasks;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace Essentieables;

/// <summary>
/// The static helpers of <c>Microsoft.Maui.ApplicationModel.Platform</c> that Avae.Windows.Essentials and Avae.macOS.Essentials
/// adapted to Avalonia. Essentieables keeps the real MAUI package, whose <c>Platform</c> class cannot be extended, so they live here.
/// <c>Platform.MapServiceToken</c> needs no replacement: it is MAUI's own on <c>net10.0-windows*</c>, the only target where the
/// MAUI geocoder (which uses it) is registered.
/// </summary>
internal static class EssentieablesPlatform
{
    /// <summary>Raises <see cref="IAppActions.AppActionActivated"/> for <paramref name="action"/> (Windows jump list).</summary>
    public static void OnLaunched(AppAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
#if ESSENTIEABLES_WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10586, 0) && AppActions.Current is OriginalAppActionsImplementation appActions)
            _ = appActions.OnLaunched(action);
#endif
    }

    /// <summary>
    /// Makes <paramref name="topLevel"/> the active window used by the pickers, the share sheet, the screenshot…; Essentieables
    /// already follows the focus, so this is only needed for a window that never gets it.
    /// </summary>
    public static void OnActivated(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        AvaeTopLevelStateManager.Current?.OnActivated(topLevel);
    }

    /// <inheritdoc cref="OnActivated(TopLevel)"/>
    public static void OnPlatformWindowInitialized(Window window) => OnActivated(window);
}

/// <summary>
/// <c>MainThread</c> on the Avalonia UI thread. The MAUI <c>MainThread</c> is static, so Essentieables cannot replace it: in an
/// Avalonia app it throws <c>NotImplementedInReferenceAssemblyException</c> (portable build) or does not know the Avalonia
/// dispatcher. Use this class or <c>Avalonia.Threading.Dispatcher.UIThread</c>.
/// </summary>
internal static class AvaeMainThread
{
    public static bool IsMainThread => Dispatcher.UIThread.CheckAccess();

    public static void BeginInvokeOnMainThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsMainThread)
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    public static Task InvokeOnMainThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsMainThread)
        {
            action();
            return Task.CompletedTask;
        }
        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    public static Task<T> InvokeOnMainThreadAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return IsMainThread ? Task.FromResult(func()) : Dispatcher.UIThread.InvokeAsync(func).GetTask();
    }

    public static Task InvokeOnMainThreadAsync(Func<Task> funcTask)
    {
        ArgumentNullException.ThrowIfNull(funcTask);
        return IsMainThread ? funcTask() : Dispatcher.UIThread.InvokeAsync(funcTask);
    }

    public static Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> funcTask)
    {
        ArgumentNullException.ThrowIfNull(funcTask);
        return IsMainThread ? funcTask() : Dispatcher.UIThread.InvokeAsync(funcTask);
    }

    public static Task<SynchronizationContext> GetMainThreadSynchronizationContextAsync()
        => InvokeOnMainThreadAsync(() => SynchronizationContext.Current!);
}
