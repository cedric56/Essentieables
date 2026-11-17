
// Portions Copyright (c) 2025 AvaloniaUI (Avalonia.Controls.Maui, MIT License). See THIRD-PARTY-NOTICES.txt.

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Maui.Essentials;
using System;
using Application = Avalonia.Application;
using Window = Avalonia.Controls.Window;

namespace Essentieables;

internal class AvaeTopLevelStateManager : IAvaloniaEssentialsPlatformProvider
{
    TopLevel? _active;

    public event EventHandler? ActiveTopLevelChanged;

    /// <summary>The manager created by the last <c>UseEssentieables()</c> call.</summary>
    internal static AvaeTopLevelStateManager? Current { get; private set; }

    static bool s_classHandlerAdded;

    // The top levels whose Closed event is already handled: a window activated again is not subscribed twice.
    readonly System.Collections.Generic.HashSet<TopLevel> _subscribed = new();

    public AvaeTopLevelStateManager()
    {
        Current = this;
        // Once per process: UseEssentieables() called twice would otherwise stack class handlers. The handler follows the
        // current manager.
        if (s_classHandlerAdded)
            return;
        s_classHandlerAdded = true;
        TopLevel.GotFocusEvent.AddClassHandler(typeof(TopLevel), (sender, args) =>
        {
            Current?.OnActivated((TopLevel)sender!);
        });
    }

    public void OnActivated(TopLevel topLevel)
    {
        if (_active == topLevel)
            return;

        _active = topLevel;
        if (_subscribed.Add(topLevel))
            topLevel.Closed += OnTopLevelClosed;

        ActiveTopLevelChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnTopLevelClosed(object? sender, EventArgs e)
    {
        if (sender is TopLevel topLevel)
        {
            topLevel.Closed -= OnTopLevelClosed;
            _subscribed.Remove(topLevel);
        }

        if (ReferenceEquals(_active, sender))
        {
            _active = null;
            ActiveTopLevelChanged?.Invoke(this, EventArgs.Empty);
        }
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
