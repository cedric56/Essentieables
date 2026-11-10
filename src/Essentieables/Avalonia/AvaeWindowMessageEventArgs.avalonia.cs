// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

#nullable enable
using System;

namespace Essentieables;

class WindowMessageEventArgs : EventArgs
{
    public WindowMessageEventArgs(IntPtr hwnd, uint messageId, IntPtr wParam, IntPtr lParam)
    {
        Hwnd = hwnd;
        MessageId = messageId;
        WParam = wParam;
        LParam = lParam;
    }

    public IntPtr Hwnd { get; }

    public uint MessageId { get; }

    public IntPtr WParam { get; }

    public IntPtr LParam { get; }

    public IntPtr Result { get; set; }

    public bool Handled { get; set; }
}
#endif
