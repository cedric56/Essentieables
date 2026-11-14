

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>
/// App info sourced from the hosting document (title, origin, theme), the entry assembly (version) and, when present, the
/// <c>&lt;meta&gt;</c> tags of index.html read by Avae.Browser.Essentials: <c>name</c> (or <c>application-name</c>),
/// <c>package</c>, <c>app-version</c> and <c>build-version</c>. JS-backed values are fetched once via <see cref="InitializeAsync"/> and kept
/// current by browser theme-change events, so the synchronous <see cref="IAppInfo"/> surface
/// never touches JS interop directly.
/// </summary>
internal sealed class BlazorAppInfo : IAppInfo, IAsyncDisposable
{
    private static readonly Version AssemblyVersion =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0);

    internal sealed record AppInfoSnapshot(
        string Title, string Hostname, bool Rtl, bool PrefersDark,
        string? MetaName = null, string? MetaPackage = null, string? MetaVersion = null, string? MetaBuild = null);

    private DotNetObjectReference<BlazorAppInfo>? _ref;

    private string _packageName = string.Empty;
    private string _name = Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    private string _versionString = AssemblyVersion.ToString();
    private Version _version = AssemblyVersion;
    private string _buildString = AssemblyVersion.Revision >= 0 ? AssemblyVersion.Revision.ToString() : "0";
    private LayoutDirection _layoutDirection = LayoutDirection.LeftToRight;
    private AppTheme _requestedTheme = AppTheme.Light;

    public async Task InitializeAsync()
    {
        try
        {
            var snapshot = await BlazorEssentials.Module.InvokeAsync<AppInfoSnapshot>("appInfoGet");
            if (snapshot != null)
                Apply(snapshot);
            // appInfoSubscribeTheme drops the previous JS handler first: dispose the old reference after it.
            var previous = _ref;
            _ref = DotNetObjectReference.Create(this);
            await BlazorEssentials.Module.InvokeVoidAsync("appInfoSubscribeTheme", _ref);
            previous?.Dispose();
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

    internal void Apply(AppInfoSnapshot s)
    {
        _packageName = !string.IsNullOrWhiteSpace(s.MetaPackage) ? s.MetaPackage.Trim() : s.Hostname;
        _name = !string.IsNullOrWhiteSpace(s.MetaName) ? s.MetaName.Trim()
            : !string.IsNullOrEmpty(s.Title) ? s.Title
            : Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
        if (!string.IsNullOrWhiteSpace(s.MetaVersion))
        {
            _versionString = s.MetaVersion.Trim();
            _version = ParseVersion(_versionString) ?? AssemblyVersion;
        }
        if (!string.IsNullOrWhiteSpace(s.MetaBuild))
            _buildString = s.MetaBuild.Trim();
        _layoutDirection = s.Rtl ? LayoutDirection.RightToLeft : LayoutDirection.LeftToRight;
        _requestedTheme = s.PrefersDark ? AppTheme.Dark : AppTheme.Light;
    }

    // ───────────────────────── IAppInfo (sync, cached) ─────────────────────────

    public string PackageName => _packageName;
    public string Name => _name;
    public string VersionString => _versionString;
    public Version Version => _version;
    public string BuildString => _buildString;

    /// <summary>"1.2.3", "1.2.3-beta+abc" → 1.2.3; "2" → 2.0; null when there is no number.</summary>
    internal static Version? ParseVersion(string value)
    {
        var core = value.Split('-', '+', ' ')[0];
        if (Version.TryParse(core, out var version))
            return version;
        return int.TryParse(core, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major)
            ? new Version(major, 0)
            : null;
    }
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