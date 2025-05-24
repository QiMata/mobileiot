using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;

public partial class P2pViewModel(IP2PService p2p) : ObservableObject
{
    [RelayCommand] async Task Discover() => await p2p.StartDiscoveryAsync();
    [RelayCommand] async Task SendPing() => await p2p.SendAsync("ping"u8.ToArray());
}
