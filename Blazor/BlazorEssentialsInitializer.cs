using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// Initializes Blazor Essentials for the current user once the component is interactive.
/// Put <c>&lt;BlazorEssentialsInitializer /&gt;</c> in an interactive root component (Routes, MainLayout…).
/// Required on Blazor Server, where every circuit is initialized on its own; on WebAssembly,
/// calling <c>BlazorEssentials.InitializeAsync(host.Services)</c> before <c>RunAsync</c> is enough.
/// </summary>
public sealed class BlazorEssentialsInitializer : ComponentBase
{
    [Inject]
    IServiceProvider Services { get; set; } = default!;

    /// <summary>Raised once the JS module is imported and the services are initialized.</summary>
    [Parameter]
    public EventCallback Initialized { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // JS interop is not available while prerendering: wait for the first interactive render.
        if (!firstRender)
            return;

        await BlazorEssentials.InitializeAsync(Services);
        await Initialized.InvokeAsync();
    }
}
