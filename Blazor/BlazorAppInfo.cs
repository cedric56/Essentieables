using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// App info sourced from the hosting document (title, origin, theme) and the entry assembly
/// (version). JS-backed values are fetched once via <see cref="InitializeAsync"/> and kept
/// current by browser theme-change events, so the synchronous <see cref="IAppInfo"/> surface
/// never touches JS interop directly.
/// </summary>
internal sealed class BlazorAppInfo : IAppInfo, IAsyncDisposable
{
    private static readonly Version AssemblyVersion =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0);

    private sealed record AppInfoSnapshot(string Title, string Hostname, bool Rtl, bool PrefersDark);

    private DotNetObjectReference<BlazorAppInfo>? _ref;

    private string _packageName = string.Empty;
    private string _name = Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    private LayoutDirection _layoutDirection = LayoutDirection.LeftToRight;
    private AppTheme _requestedTheme = AppTheme.Light;

    public async Task InitializeAsync()
    {
        try
        {
            var snapshot = await BlazorEssentials.Module.InvokeAsync<AppInfoSnapshot>("appInfoGet");
            if (snapshot != null)
                Apply(snapshot);
            _ref?.Dispose();
            _ref = DotNetObjectReference.Create(this);
            await BlazorEssentials.Module.InvokeVoidAsync("appInfoSubscribeTheme", _ref);
        }        
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif

    }

    [JSInvokable]
    public void OnThemeChanged(bool prefersDark)
        => _requestedTheme = prefersDark ? AppTheme.Dark : AppTheme.Light;

    private void Apply(AppInfoSnapshot s)
    {
        _packageName = s.Hostname;
        _name = string.IsNullOrEmpty(s.Title)
            ? Assembly.GetEntryAssembly()?.GetName().Name ?? "App"
            : s.Title;
        _layoutDirection = s.Rtl ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
        _requestedTheme = s.PrefersDark ? AppTheme.Dark : AppTheme.Light;
    }

    // ───────────────────────── IAppInfo (sync, cached) ─────────────────────────

    public string PackageName => _packageName;
    public string Name => _name;
    public string VersionString => AssemblyVersion.ToString();
    public Version Version => AssemblyVersion;
    public string BuildString =>
        AssemblyVersion.Revision >= 0 ? AssemblyVersion.Revision.ToString() : "0";
    public AppTheme RequestedTheme => _requestedTheme;
    public AppPackagingModel PackagingModel => AppPackagingModel.Unpackaged;
    public LayoutDirection RequestedLayoutDirection => _layoutDirection;

    public void ShowSettingsUI() =>
        throw new FeatureNotSupportedException("Browsers do not expose an app settings UI.");

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        try
        {
            await BlazorEssentials.Module.InvokeVoidAsync("appInfoUnsubscribeTheme");
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
#else
        catch
        {

        }
#endif
    }
}