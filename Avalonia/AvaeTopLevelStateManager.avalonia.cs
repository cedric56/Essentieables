using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Maui.Essentials;
using System;
using Application = Avalonia.Application;

namespace Essentieables;

public class AvaeTopLevelStateManager : IAvaloniaEssentialsPlatformProvider
{
    TopLevel? _active;

    public event EventHandler? ActiveTopLevelChanged;

    public AvaeTopLevelStateManager()
    {
        TopLevel.GotFocusEvent.AddClassHandler(typeof(TopLevel), (sender, args) =>
        {
            OnActivated((TopLevel)sender!);
        });
    }

    public void OnActivated(TopLevel topLevel)
    {
        if (_active == topLevel)
            return;

        _active = topLevel;

        ActiveTopLevelChanged?.Invoke(this, EventArgs.Empty);
    }

    public TopLevel? GetTopLevel()
    {
        var lifetime = Application.Current?.ApplicationLifetime;
        var active = lifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => _active ?? TopLevel.GetTopLevel(desktop.MainWindow),
            ISingleViewApplicationLifetime singleView => _active ?? TopLevel.GetTopLevel(singleView.MainView),
            _ => _active ?? TopLevel.GetTopLevel(null)
        };

        return active ?? TopLevel.GetTopLevel(null);
    }

    /// <summary>
    /// Gets the application's currently active window's pointer.
    /// </summary>
    /// <param name="manager">The object to invoke this method on.</param>
    /// <param name="throwOnNull">Throws an exception if no current <see cref="Window"/> can be found and this value is set to <see langword="true"/>, otherwise this method returns <see cref="IntPtr.Zero"/>.</param>
    /// <returns>The application's currently active window's <see cref="IntPtr"/>.</returns>
    /// <exception cref="NullReferenceException">Thrown if no current <see cref="Window"/> can be found and <paramref name="throwOnNull"/> is set to <see langword="true"/>.</exception>
    public IntPtr GetActiveWindowHandle(bool throwOnNull)
    {
        var window = GetTopLevel() as Window;
        if (throwOnNull && window == null)
            throw new NullReferenceException("The active Window cannot be detected. Ensure that you have called Init in your Application class.");

        if (window == null)
            return IntPtr.Zero;

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        return handle;
    }
}
