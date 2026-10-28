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
