// Windows only: compiled where ESSENTIEABLES_WINDOWS is defined (net10.0 and net10.0-windows*, see build/Essentieables.targets),
// so that Android, iOS, macOS and browser apps need no Windows contract package.
#if ESSENTIEABLES_WINDOWS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using System;
using System.Threading;

namespace Essentieables
{
    static class Utils
    {
        internal static CancellationToken TimeoutToken(CancellationToken cancellationToken, TimeSpan timeout)
        {
            // create a new linked cancellation token source
            var cancelTokenSrc = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // if a timeout was given, make the token source cancel after it expires
            if (timeout > TimeSpan.Zero)
                cancelTokenSrc.CancelAfter(timeout);

            // our Cancel method will handle the actual cancellation logic
            return cancelTokenSrc.Token;
        }
    }
}
#endif
