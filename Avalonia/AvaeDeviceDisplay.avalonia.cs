#nullable enable
using Microsoft.Maui.Devices;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Graphics.Display;
using Windows.System.Display;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace Essentieables;

abstract class DeviceDisplayImplementationBase : IDeviceDisplay
{
    event EventHandler<DisplayInfoChangedEventArgs>? MainDisplayInfoChangedInternal;

    DisplayInfo _currentMetrics;

    public DisplayInfo MainDisplayInfo => GetMainDisplayInfo();

    public bool KeepScreenOn
    {
        get => GetKeepScreenOn();
        set => SetKeepScreenOn(value);
    }

    public event EventHandler<DisplayInfoChangedEventArgs> MainDisplayInfoChanged
    {
        add
        {
            if (MainDisplayInfoChangedInternal is null)
            {
                SetCurrent(MainDisplayInfo);
                StartScreenMetricsListeners();
            }
            MainDisplayInfoChangedInternal += value;
        }
        remove
        {
            var wasStopped = MainDisplayInfoChangedInternal is null;
            MainDisplayInfoChangedInternal -= value;
            if (!wasStopped && MainDisplayInfoChangedInternal is null)
                StopScreenMetricsListeners();
        }
    }

    void SetCurrent(DisplayInfo metrics) =>
        _currentMetrics = new DisplayInfo(
            metrics.Width, metrics.Height,
            metrics.Density,
            metrics.Orientation,
            metrics.Rotation,
            metrics.RefreshRate);

    protected void OnMainDisplayInfoChanged(DisplayInfoChangedEventArgs e)
    {
        if (!_currentMetrics.Equals(e.DisplayInfo))
        {
            SetCurrent(e.DisplayInfo);
            MainDisplayInfoChangedInternal?.Invoke(null, e);
        }
    }

    protected void OnMainDisplayInfoChanged()
    {
        var metrics = GetMainDisplayInfo();
        OnMainDisplayInfoChanged(new DisplayInfoChangedEventArgs(metrics));
    }

    protected abstract DisplayInfo GetMainDisplayInfo();

    protected abstract bool GetKeepScreenOn();

    protected abstract void SetKeepScreenOn(bool keepScreenOn);

    protected abstract void StartScreenMetricsListeners();

    protected abstract void StopScreenMetricsListeners();
}

sealed partial class AvaeDeviceDisplay : DeviceDisplayImplementationBase
{
}

[SupportedOSPlatform("windows10.0.10240")]
partial class AvaeDeviceDisplay
{
    readonly object locker = new object();
    readonly AvaeActiveWindowTracker _activeWindowTracker;
    readonly AvaeTopLevelStateManager _topLevelStateManager;

    DisplayRequest? displayRequest;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaeDeviceDisplay"/> class.
    /// </summary>
    public AvaeDeviceDisplay(AvaeTopLevelStateManager topLevelStateManager)
    {
        _topLevelStateManager = topLevelStateManager;
        _activeWindowTracker = new(topLevelStateManager);
        _activeWindowTracker.WindowMessage += OnWindowMessage;
    }

    protected override bool GetKeepScreenOn()
    {
        lock (locker)
        {
            return displayRequest != null;
        }
    }

    protected override void SetKeepScreenOn(bool keepScreenOn)
    {
        lock (locker)
        {
            if (keepScreenOn)
            {
                if (displayRequest == null)
                {
                    displayRequest = new DisplayRequest();
                    displayRequest.RequestActive();
                }
            }
            else
            {
                if (displayRequest != null)
                {
                    displayRequest.RequestRelease();
                    displayRequest = null;
                }
            }
        }
    }

    protected override DisplayInfo GetMainDisplayInfo()
    {
        var windowHandle = _topLevelStateManager.GetActiveWindowHandle(false);
        var mi = GetDisplay(windowHandle);

        if (mi == null)
            return default;

        var vDevMode = new DEVMODE();
        EnumDisplaySettings(mi.Value.DeviceNameToLPTStr(), -1, ref vDevMode);

        var w = vDevMode.dmPelsWidth;
        var h = vDevMode.dmPelsHeight;

        //var dpi = (double)GetDpiForWindow(windowHandle);
        //if (dpi != 0)
        //	dpi /= DeviceDisplay.BaseLogicalDpi;
        //else
        //	dpi = 1.0;
        const double BASE_LOGICAL_DPI = 96.0;
        var dpi = (double)GetDpiForWindow(windowHandle);
        if (dpi != 0)
            dpi /= BASE_LOGICAL_DPI;
        else
            dpi = 1.0;

        var displayOrientation = GetDisplayOrientation(vDevMode);
        var rotation = CalculateRotation(displayOrientation);

        var orientation = displayOrientation == DisplayOrientations.Landscape || displayOrientation == DisplayOrientations.LandscapeFlipped
            ? DisplayOrientation.Landscape
            : DisplayOrientation.Portrait;

        return new DisplayInfo(
            width: w,
            height: h,
            density: dpi,
            orientation: orientation,
            rotation: rotation,
            rate: vDevMode.dmDisplayFrequency);
    }

    static MONITORINFOEX? GetDisplay(IntPtr hwnd)
    {
        IntPtr hMonitor;
        RECT rc;
        GetWindowRect(hwnd, out rc);
        hMonitor = MonitorFromRect(ref rc, MonitorOptions.MONITOR_DEFAULTTONEAREST);

        MONITORINFOEX mi = new MONITORINFOEX();
        mi.Size = Marshal.SizeOf(mi);
        bool success = GetMonitorInfo(hMonitor, ref mi);
        if (success)
        {
            return mi;
        }
        return null;
    }

    protected override void StartScreenMetricsListeners() =>
        Dispatcher.UIThread.Invoke(_activeWindowTracker.Start);

    protected override void StopScreenMetricsListeners() =>
        Dispatcher.UIThread.Invoke(_activeWindowTracker.Stop);

    // Currently there isn't a way to detect Orientation Changes unless you subclass the WinUI.Window and watch the messages.
    // This is the "subtlest" way to currently wire this together.
    // Hopefully there will be a more public API for this down the road so we can just use that directly from Essentials
    void OnWindowMessage(object? sender, WindowMessageEventArgs e)
    {
        if (e.MessageId == PlatformMethods.MessageIds.WM_DISPLAYCHANGE ||
            e.MessageId == PlatformMethods.MessageIds.WM_DPICHANGED ||
            e.MessageId == PlatformMethods.MessageIds.WM_WINDOWPOSCHANGED)
            OnMainDisplayInfoChanged();
    }

    static DisplayRotation CalculateRotation(DisplayOrientations orientation) =>
        orientation switch
        {
            DisplayOrientations.Landscape => DisplayRotation.Rotation0,
            DisplayOrientations.Portrait => DisplayRotation.Rotation270,
            DisplayOrientations.LandscapeFlipped => DisplayRotation.Rotation180,
            DisplayOrientations.PortraitFlipped => DisplayRotation.Rotation90,
            _ => DisplayRotation.Rotation0,
        };

    static DisplayOrientations GetDisplayOrientation(DEVMODE devMode) =>
        devMode.dmDisplayOrientation switch
        {
            0 => DisplayOrientations.Landscape,
            1 => DisplayOrientations.Portrait,
            2 => DisplayOrientations.LandscapeFlipped,
            3 => DisplayOrientations.PortraitFlipped,
            _ => DisplayOrientations.Landscape,
        };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr MonitorFromRect(ref RECT lprc, MonitorOptions dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumDisplaySettings(
        byte[] lpszDeviceName,
        [param: MarshalAs(UnmanagedType.U4)] int iModeNum,
        [In, Out] ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    enum MonitorOptions : uint
    {
        MONITOR_DEFAULTTONULL,
        MONITOR_DEFAULTTOPRIMARY,
        MONITOR_DEFAULTTONEAREST
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX
    {
        public int Size;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string DeviceName;

        public byte[] DeviceNameToLPTStr()
        {
            var lptArray = new byte[DeviceName.Length + 1];

            var index = 0;
            foreach (char c in DeviceName.ToCharArray())
                lptArray[index++] = Convert.ToByte(c);

            lptArray[index] = Convert.ToByte('\0');

            return lptArray;
        }
    }

    struct DEVMODE
    {
        const int CCHDEVICENAME = 0x20;
        const int CCHFORMNAME = 0x20;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
