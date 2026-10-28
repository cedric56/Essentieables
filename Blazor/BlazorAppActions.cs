using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>App shortcuts have no web equivalent.</summary>
public class BlazorAppActions : IAppActions
{
    public bool IsSupported => false;

    public event EventHandler<AppActionEventArgs>? AppActionActivated
    {
        add { }
        remove { }
    }

    public Task<IEnumerable<AppAction>> GetAsync() =>
        throw new FeatureNotSupportedException("App actions are not supported in the browser.");

    public Task SetAsync(IEnumerable<AppAction> actions) =>
        throw new FeatureNotSupportedException("App actions are not supported in the browser.");
}
