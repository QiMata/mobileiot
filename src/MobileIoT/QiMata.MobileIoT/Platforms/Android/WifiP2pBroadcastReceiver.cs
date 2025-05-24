using Android.Content;
using Android.Net.Wifi.P2p;

namespace QiMata.MobileIoT.Platforms.Android;

sealed class WifiP2pBroadcastReceiver : BroadcastReceiver
{
    readonly WifiP2pManager _manager;
    readonly WifiP2pManager.Channel _channel;
    readonly WifiP2pManager.IPeerListListener _peers;
    readonly WifiP2pManager.IConnectionInfoListener _conn;

    public WifiP2pBroadcastReceiver(
        WifiP2pManager manager,
        WifiP2pManager.Channel channel,
        WifiP2pManager.IPeerListListener peers,
        WifiP2pManager.IConnectionInfoListener conn)
    {
        _manager = manager;
        _channel = channel;
        _peers = peers;
        _conn = conn;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == WifiP2pManager.WifiP2pPeersChangedAction)
        {
            _manager.RequestPeers(_channel, _peers);
        }
        else if (intent?.Action == WifiP2pManager.WifiP2pConnectionChangedAction)
        {
            _manager.RequestConnectionInfo(_channel, _conn);
        }
    }
}
