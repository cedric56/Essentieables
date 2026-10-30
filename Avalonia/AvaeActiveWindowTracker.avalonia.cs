// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

#nullable enable

using Avalonia.Controls;
using System;
using Window = Avalonia.Controls.Window;

namespace Essentieables;

class AvaeActiveWindowTracker
{
    readonly AvaeTopLevelStateManager _windowStateManager;

    AvaeWindowMessageManager? _currentWindowManager;

    public AvaeActiveWindowTracker(AvaeTopLevelStateManager windowStateManager)
    {
        _windowStateManager = windowStateManager;
    }

    public event EventHandler<WindowMessageEventArgs>? WindowMessage;

    public void Start()
    {
        var window = _windowStateManager.GetTopLevel();
        OnActiveWindowChanged(window);

        _windowStateManager.ActiveTopLevelChanged += OnActiveWindowChanged;
    }

    public void Stop()
    {
        OnActiveWindowChanged(null);

        _windowStateManager.ActiveTopLevelChanged -= OnActiveWindowChanged;
    }

    void OnActiveWindowChanged(object? sender, EventArgs e)
    {
        var window = _windowStateManager?.GetTopLevel();
        OnActiveWindowChanged(window);
    }

    void OnActiveWindowChanged(TopLevel? window)
    {
        if (_currentWindowManager is not null)
        {
            _currentWindowManager.WindowMessage -= OnWindowMessage;
            _currentWindowManager = null;
        }

        if (window is Window w)
        {
            _currentWindowManager = AvaeWindowMessageManager.Get(w);
            _currentWindowManager.WindowMessage += OnWindowMessage;
        }
    }

    void OnWindowMessage(object? sender, WindowMessageEventArgs e) =>
        WindowMessage?.Invoke(sender, e);
}
