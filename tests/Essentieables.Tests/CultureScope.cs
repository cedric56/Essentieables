using System;
using System.Globalization;

namespace Essentieables.Tests;

/// <summary>Temporarily switches the current culture (thread-local, so safe in parallel tests).</summary>
sealed class CultureScope : IDisposable
{
    readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public CultureScope(string name)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
