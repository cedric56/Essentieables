// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

using Microsoft.Maui.ApplicationModel;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Dispatcher = Avalonia.Threading.Dispatcher;
using DispatcherPriority = Avalonia.Threading.DispatcherPriority;
using Window = Avalonia.Controls.Window;

namespace Essentieables;

/// <summary>
/// Raises <see cref="IAppActions.AppActionActivated"/> when the app is launched from a jump list item, as the former
/// <c>UseMauiEssentials()</c> of Avae.Windows.Essentials did:
/// <list type="bullet">
/// <item>the jump list starts the executable with the <c>XE_APP_ACTIONS-…</c> argument;</item>
/// <item>the action is raised once the first window is open, after the app subscribed to the event;</item>
/// <item>single instance, opt-in: <see cref="TryForwardToRunningInstance"/>, called by the app in <c>Main</c>, passes the action
/// to a running instance of the same executable (WM_COPYDATA to its main window), which raises it; the app then returns.</item>
/// </list>
/// </summary>
/// <example>
/// <code>
/// public static void Main(string[] args)
/// {
///     if (AvaeAppActionsActivation.TryForwardToRunningInstance())
///         return; // the running instance handles the jump list action
///     BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
/// }
/// </code>
/// </example>
[SupportedOSPlatform("windows10.0.10586")]
static class AvaeAppActionsActivation
{
    const int WM_COPYDATA = 0x004A;

    // dwData of our WM_COPYDATA messages ("ESSA"): other WM_COPYDATA messages the window receives are left alone.
    static readonly IntPtr CopyDataMarker = new(0x45535341);

    static OriginalAppActionsImplementation? s_appActions;
    static string? s_pendingArgument;
    static bool s_initialized;

    /// <summary>Activation handling. Called by <c>UseEssentieables()</c>; never ends the process.</summary>
    internal static void Initialize(OriginalAppActionsImplementation appActions)
    {
        s_appActions = appActions;
        if (s_initialized)
            return;
        s_initialized = true;

        s_pendingArgument = GetLaunchArgument();

        // Every window can receive the argument: the sender targets Process.MainWindowHandle, the first visible top-level window.
        Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (sender, _) => OnWindowOpened((Window)sender!));
    }

    static string? GetLaunchArgument() => Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.StartsWith(OriginalAppActionsExtensions.AppActionPrefix, StringComparison.Ordinal));

    /// <summary>
    /// When the app was launched from a jump list item and another instance of the same executable runs, hands the action to it
    /// and returns true: the caller (<c>Main</c>) should then exit. Returns false otherwise, and always when the app runs
    /// through the <c>dotnet</c> host (<c>dotnet app.dll</c>), where other apps share the process name and path.
    /// </summary>
    public static bool TryForwardToRunningInstance()
    {
        var argument = GetLaunchArgument();
        return argument is not null && TrySendToRunningInstance(argument);
    }

    static void OnWindowOpened(Window window)
    {
        try
        {
            AvaeWindowMessageManager.Get(window).WindowMessage += OnWindowMessage;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        if (s_pendingArgument is { } argument)
        {
            s_pendingArgument = null;
            // Posted: the handlers of WindowOpened, where apps often subscribe, run first.
            Dispatcher.UIThread.Post(() => _ = ActivateAsync(argument), DispatcherPriority.Background);
        }
    }

    static void OnWindowMessage(object? sender, WindowMessageEventArgs e)
    {
        if (e.MessageId != WM_COPYDATA)
            return;

        var data = Marshal.PtrToStructure<COPYDATASTRUCT>(e.LParam);
        if (data.dwData != CopyDataMarker || data.lpData == IntPtr.Zero || data.cbData <= 0)
            return;

        var argument = Marshal.PtrToStringUni(data.lpData, data.cbData / 2).TrimEnd('\0');
        e.Handled = true;
        e.Result = new IntPtr(1);

        // Out of the window procedure: the sender waits in SendMessage until it returns.
        Dispatcher.UIThread.Post(() => _ = ActivateAsync(argument));
    }

    /// <summary>Raises AppActionActivated for the jump list argument <paramref name="argument"/>.</summary>
    internal static async Task ActivateAsync(string argument)
    {
        var appActions = s_appActions;
        if (appActions is null)
            return;

        try
        {
            var id = argument.ArgumentsToId();
            if (string.IsNullOrEmpty(id))
                return;

            // The title and subtitle come from the jump list; an unknown id is still reported.
            var actions = await appActions.GetAsync();
            var action = actions.FirstOrDefault(a => a.Id == id) ?? new AppAction(id, id);
            await appActions.OnLaunched(action);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    static bool TrySendToRunningInstance(string argument)
    {
        // The instance is identified by its executable: under the dotnet host, every app is "dotnet.exe".
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable)
            || string.Equals(System.IO.Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            return false;

        using var current = Process.GetCurrentProcess();
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(current.ProcessName);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                if (process.Id == current.Id)
                    continue;

                IntPtr window;
                try
                {
                    // Same name is not enough: another app may use it. Same executable file is.
                    var path = process.MainModule?.FileName;
                    if (!string.Equals(path, executable, StringComparison.OrdinalIgnoreCase))
                        continue;
                    window = process.MainWindowHandle;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    continue; // exited meanwhile, or not accessible (other user, elevated)
                }

                if (window != IntPtr.Zero && Send(window, argument))
                    return true;
            }
            return false;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    static bool Send(IntPtr window, string argument)
    {
        var data = Marshal.StringToHGlobalUni(argument);
        try
        {
            var copyData = new COPYDATASTRUCT
            {
                dwData = CopyDataMarker,
                cbData = (argument.Length + 1) * 2, // UTF-16, terminating null included
                lpData = data,
            };
            return SendMessage(window, WM_COPYDATA, IntPtr.Zero, ref copyData) != IntPtr.Zero;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);
}
#endif
