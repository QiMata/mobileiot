using Foundation;
using MultipeerConnectivity;
using QiMata.MobileIoT.Services;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class MultipeerService : NSObject, IP2PService, IMCSessionDelegate,
                                        IMCNearbyServiceAdvertiserDelegate, IMCNearbyServiceBrowserDelegate
{
    readonly string _svcType = "maui-p2p";
    readonly MCPeerID _self;
    readonly MCSession _session;
    readonly MCNearbyServiceAdvertiser _adv;
    readonly MCNearbyServiceBrowser _browser;
    readonly Channel<ReadOnlyMemory<byte>> _inbound = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();

    public MultipeerService()
    {
        _self = new MCPeerID(UIDevice.CurrentDevice.Name);
        _session = new MCSession(_self, null, MCEncryptionPreference.Required);
        _session.Delegate = this;

        _adv = new MCNearbyServiceAdvertiser(_self, null, _svcType);
        _adv.Delegate = this;

        _browser = new MCNearbyServiceBrowser(_self, _svcType);
        _browser.Delegate = this;
    }

    public Task<bool> StartDiscoveryAsync(CancellationToken ct = default)
    {
        _adv.StartAdvertisingPeer();
        _browser.StartBrowsingForPeers();
        return Task.FromResult(true);
    }

    public Task<bool> ConnectToPeerAsync(string peerId, CancellationToken ct = default)
    {
        // Multipeer connects via invitations; browser sends invite automatically
        return Task.FromResult(true);
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> buffer, string? peerId = null, CancellationToken ct = default)
    {
        var peers = _session.ConnectedPeers;
        if (peers.Length == 0) return false;

        var data = NSData.FromArray(buffer.ToArray());
        NSError? err;
        _session.SendData(data, peers, MCSessionSendDataMode.Reliable, out err);
        return err is null;
    }

    public async IAsyncEnumerable<(string PeerId, ReadOnlyMemory<byte> Data)> ReceiveAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (await _inbound.Reader.WaitToReadAsync(ct))
        {
            while (_inbound.Reader.TryRead(out var mem))
                yield return ("peer", mem);
        }
    }

    public Task StopAsync()
    {
        _adv.StopAdvertisingPeer();
        _browser.StopBrowsingForPeers();
        _session.Disconnect();
        return Task.CompletedTask;
    }

    // ---- Advertiser ----
    public void DidReceiveInvitationFromPeer(MCNearbyServiceAdvertiser advertiser, MCPeerID peerID,
                                             NSData? context, MCNearbyServiceInvitationHandler invitationHandler)
        => invitationHandler(true, _session);

    // ---- Browser ----
    public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary info)
        => browser.InvitePeer(peerID, _session, null, 20);

    public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID) { }

    // ---- Session ----
    public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
        => _inbound.Writer.TryWrite(data.ToArray());

    public void PeerChangedState(MCSession session, MCPeerID peerID, MCSessionState state) { }
    public void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID) { }
    public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID peerID,
                                          NSProgress progress) { }
    public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID peerID,
                                           NSUrl localUrl, NSError? error) { }
}
