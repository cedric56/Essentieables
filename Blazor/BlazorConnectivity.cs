using Essentieables;
using Microsoft.JSInterop;
using Microsoft.Maui.Networking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Essentieables;
public sealed class BlazorConnectivity : IConnectivity, IAsyncDisposable
{
    public sealed record BrowserState(bool Online, string Type, bool SaveData);

    private volatile State _state = State.Unknown;
    private DotNetObjectReference<BlazorConnectivity>? _ref;

    public NetworkAccess NetworkAccess => _state.Access;
    public IEnumerable<ConnectionProfile> ConnectionProfiles => _state.Profiles;
    public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

    // Idempotent: safe to call from several places.
    public async Task InitializeAsync()
    {
        try
        {
            var snapshot = await BlazorEssentials.Module.InvokeAsync<BrowserState?>("connectivityGetSnapshot");
            if (snapshot is not null)
                Apply(snapshot, false);
            _ref?.Dispose();
            _ref = DotNetObjectReference.Create(this);
            await BlazorEssentials.Module.InvokeVoidAsync("connectivitySubscribe", _ref);
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
    public void OnBrowserChanged(BrowserState s) => Apply(s, raise: true);

    private void Apply(BrowserState s, bool raise)
    {
        var access = !s.Online ? NetworkAccess.None
                   : s.SaveData ? NetworkAccess.ConstrainedInternet
                   : NetworkAccess.Internet;

        var profile = s.Type switch
        {
            "wifi" => ConnectionProfile.WiFi,
            "cellular" => ConnectionProfile.Cellular,
            "ethernet" => ConnectionProfile.Ethernet,
            "bluetooth" => ConnectionProfile.Bluetooth,
            _ => ConnectionProfile.Unknown
        };

        var profiles = s.Online ? new[] { profile } : Array.Empty<ConnectionProfile>();
        _state = new State(access, profiles);

        if (raise)
            ConnectivityChanged?.Invoke(this, new ConnectivityChangedEventArgs(access, profiles));
    }

    private sealed record State(NetworkAccess Access, IReadOnlyList<ConnectionProfile> Profiles)
    {
        public static readonly State Unknown = new(NetworkAccess.Unknown, Array.Empty<ConnectionProfile>());
    }

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        try
        { 
            await BlazorEssentials.Module.InvokeVoidAsync("connectivityUnsubscribe");   
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