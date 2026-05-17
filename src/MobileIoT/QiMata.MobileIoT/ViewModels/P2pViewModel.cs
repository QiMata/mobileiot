using QiMata.MobileIoT.Shared.Services;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.ViewModels;

public partial class P2pViewModel(IP2PService p2p, IAppLogger logger) : BaseViewModel(logger)
{
    [RelayCommand] async Task Discover() => await p2p.StartDiscoveryAsync();
    [RelayCommand] async Task SendPing() => await p2p.SendAsync("ping"u8.ToArray());

    [RelayCommand] async Task ConnectToPeer(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
            return;
        await p2p.ConnectToPeerAsync(peerId);
    }

    [RelayCommand] async Task StopDiscovery() => await p2p.StopAsync();

    public record PeerMessage(string PeerId, string Message);

    [RelayCommand] async Task SendToPeer(PeerMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.PeerId) || string.IsNullOrWhiteSpace(message.Message))
            return;
        await p2p.SendAsync(Encoding.UTF8.GetBytes(message.Message), peerId: message.PeerId);
    }
}
