using Microsoft.Maui.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using Windows.Networking.Connectivity;
using ConnectionProfile = Microsoft.Maui.Networking.ConnectionProfile;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace Essentieables;

partial class OriginalConnectivityImplementation : IConnectivity
{
    event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChangedInternal;

    // a cache so that events aren't fired unnecessarily
    // this is mainly an issue on Android, but we can still do this Essentials.Avalonia
    NetworkAccess currentAccess;
    List<ConnectionProfile> currentProfiles = [];

    public event EventHandler<ConnectivityChangedEventArgs> ConnectivityChanged
    {
        add
        {
            if (ConnectivityChangedInternal is null)
            {
                SetCurrent();
                StartListeners();
            }
            ConnectivityChangedInternal += value;
        }
        remove
        {
            ConnectivityChangedInternal -= value;
            if (ConnectivityChangedInternal is null)
                StopListeners();
        }
    }

    void SetCurrent()
    {
        currentAccess = NetworkAccess;
        currentProfiles = new List<ConnectionProfile>(ConnectionProfiles);
    }

    void OnConnectivityChanged(NetworkAccess access, IEnumerable<ConnectionProfile> profiles)
        => OnConnectivityChanged(new ConnectivityChangedEventArgs(access, profiles));

    void OnConnectivityChanged()
        => OnConnectivityChanged(NetworkAccess, ConnectionProfiles);

    void OnConnectivityChanged(ConnectivityChangedEventArgs e)
    {
        if (currentAccess != e.NetworkAccess || !currentProfiles.SequenceEqual<ConnectionProfile>(e.ConnectionProfiles))
        {
            SetCurrent();
            Dispatcher.UIThread.Invoke(() => ConnectivityChangedInternal?.Invoke(null, e));
        }
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalConnectivityImplementation : IConnectivity
{
    void StartListeners() =>
        NetworkInformation.NetworkStatusChanged += NetworkStatusChanged;

    void StopListeners() =>
        NetworkInformation.NetworkStatusChanged -= NetworkStatusChanged;

    void NetworkStatusChanged(object sender) =>
        OnConnectivityChanged();

    public NetworkAccess NetworkAccess
    {
        get
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000, 0))
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile == null)
                    return NetworkAccess.Unknown;

                var level = profile.GetNetworkConnectivityLevel();
                return level switch
                {
                    NetworkConnectivityLevel.LocalAccess => NetworkAccess.Local,
                    NetworkConnectivityLevel.InternetAccess => NetworkAccess.Internet,
                    NetworkConnectivityLevel.ConstrainedInternetAccess => NetworkAccess.ConstrainedInternet,
                    _ => NetworkAccess.None,
                };
            }
            else
            {
                // Windows 10 workaround for https://github.com/microsoft/WindowsAppSDK/issues/2965
                var networkList = OriginalConnectivityNativeHelper.GetNetworkListManager();
                var enumNetworks = networkList?.GetNetworks(OriginalConnectivityNativeHelper.NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED);
                var connectivity = OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_DISCONNECTED;


                if (enumNetworks != null)
                {
                    foreach (OriginalConnectivityNativeHelper.INetwork networkInterface in enumNetworks)
                    {
                        if (networkInterface.IsConnected())
                        {
                            connectivity = networkInterface.GetConnectivity();
                            break;
                        }
                    }
                }
                if ((connectivity & (OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_INTERNET | OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_INTERNET)) != 0)
                {
                    return NetworkAccess.Internet;
                }
                else if ((connectivity & (OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_LOCALNETWORK | OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_LOCALNETWORK)) != 0)
                {
                    return NetworkAccess.Local;
                }
                else if ((connectivity & (OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_NOTRAFFIC | OriginalConnectivityNativeHelper.NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_NOTRAFFIC)) != 0)
                {
                    return NetworkAccess.Local;
                }
                else
                {
                    return NetworkAccess.None;
                }
            }
        }
    }

    public IEnumerable<ConnectionProfile> ConnectionProfiles
    {
        get
        {
            NetworkInterface[] networkInterfaces = [];
            try
            {
                networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException ex)
            {
                Debug.WriteLine($"Unable to get network interfaces. Error: {ex.Message}");
            }

            foreach (var nic in networkInterfaces)
            {
                if (nic.OperationalStatus is not OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var interfaceType = ConnectionProfile.Unknown;
                switch (nic.NetworkInterfaceType)
                {
                    case NetworkInterfaceType.Ethernet:
                        interfaceType = ConnectionProfile.Ethernet;
                        break;
                    case NetworkInterfaceType.Wireless80211:
                        interfaceType = ConnectionProfile.WiFi;
                        break;
                    case NetworkInterfaceType.Wwanpp:
                    case NetworkInterfaceType.Wwanpp2:
                        interfaceType = ConnectionProfile.Cellular;
                        break;
                }

                yield return interfaceType;
            }
        }
    }
}
