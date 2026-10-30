// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

using Microsoft.Maui.Dispatching;
using System;
using System.Collections.Generic;
using System.Text;
using Dispatcher = Avalonia.Threading.Dispatcher;
using DispatcherTimer = Avalonia.Threading.DispatcherTimer;
using DispatcherPriority = Avalonia.Threading.DispatcherPriority;

namespace Essentieables;

internal class AvaeDispatcher : IDispatcher
{
    private static IDispatcher? dispatcher;
    public static IDispatcher Default => dispatcher ?? Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread() ?? throw new ArgumentNullException(nameof(dispatcher));

    public static void Initialize(Dispatcher avaloniaDispatcher)
    {
        dispatcher = new AvaeDispatcher(avaloniaDispatcher);
    }

    private readonly Dispatcher _avaloniaDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaeDispatcher"/> class.
    /// </summary>
    /// <param name="avaloniaDispatcher">The Avalonia dispatcher instance to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="avaloniaDispatcher"/> is null.</exception>
    private AvaeDispatcher(Dispatcher avaloniaDispatcher)
    {
        _avaloniaDispatcher = avaloniaDispatcher ?? throw new ArgumentNullException(nameof(avaloniaDispatcher));
    }

    /// <summary>
    /// Gets a value indicating whether a dispatch is required to execute code on the UI thread.
    /// </summary>
    public bool IsDispatchRequired => !_avaloniaDispatcher.CheckAccess();

    /// <summary>
    /// Dispatches the specified action to be executed on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>Always returns true.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public bool Dispatch(Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        _avaloniaDispatcher.Post(action);
        return true;
    }

    /// <summary>
    /// Dispatches the specified action to be executed on the UI thread after the specified delay.
    /// </summary>
    /// <param name="delay">The time delay before executing the action.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>Always returns true.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        _avaloniaDispatcher.Post(() =>
        {
            DispatcherTimer.RunOnce(action, delay, DispatcherPriority.Normal);

        }, DispatcherPriority.Normal);
        return true;
    }

    /// <summary>
    /// Creates a new dispatcher timer.
    /// </summary>
    /// <returns>A new instance of <see cref="IDispatcherTimer"/>.</returns>
    public IDispatcherTimer CreateTimer()
    {
        return new AvaloniaDispatcherTimer(_avaloniaDispatcher);
    }
}

/// <summary>
/// Avalonia implementation of MAUI's IDispatcherTimer
/// </summary>
internal class AvaloniaDispatcherTimer : IDispatcherTimer
{
    private readonly DispatcherTimer _avaloniaTimer;
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaDispatcherTimer"/> class.
    /// </summary>
    /// <param name="dispatcher">The Avalonia dispatcher instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher"/> is null.</exception>
    public AvaloniaDispatcherTimer(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _avaloniaTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _avaloniaTimer.Tick += OnTick;
    }

    /// <summary>
    /// Gets or sets the interval between timer ticks.
    /// </summary>
    public TimeSpan Interval
    {
        get => _avaloniaTimer.Interval;
        set => _avaloniaTimer.Interval = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the timer repeats after each tick.
    /// </summary>
    public bool IsRepeating { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether the timer is currently running.
    /// </summary>
    public bool IsRunning => _avaloniaTimer.IsEnabled;

    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    public event EventHandler? Tick;

    /// <summary>
    /// Starts the timer.
    /// </summary>
    public void Start()
    {
        _avaloniaTimer.Start();
    }

    /// <summary>
    /// Stops the timer.
    /// </summary>
    public void Stop()
    {
        _avaloniaTimer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Tick?.Invoke(this, e);

        if (!IsRepeating)
        {
            Stop();
        }
    }
}
