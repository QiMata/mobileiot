using Android.Content;
using Android.Net.Wifi.P2p;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Platforms.Android.Helpers;
using Android.App;
using Microsoft.Maui.ApplicationModel;
using Java.Net;
using Android.OS;
using Android.Runtime;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class WifiDirectService : Java.Lang.Object, IP2PService,
    WifiP2pManager.IActionListener,
    WifiP2pManager.IPeerListListener,
    WifiP2pManager.IConnectionInfoListener
{
    readonly WifiP2pManager _manager;
    readonly WifiP2pManager.Channel _channel;
    readonly BroadcastReceiver _receiver;
    readonly Context _ctx = Application.Context;
    TaskCompletionSource<bool>? _pendingDiscover;
    TaskCompletionSource<bool>? _pendingConnect;
    Socket? _socket;
    CancellationTokenSource? _recvCts;

    public WifiDirectService()
    {
        _manager = WifiP2pManager.FromContext(_ctx)!;
        _channel = _manager.Initialize(_ctx, Looper.MainLooper, null);
        _receiver = new WifiP2pBroadcastReceiver(_manager, _channel, this, this);
        var filter = new IntentFilter(WifiP2pManager.WifiP2pPeersChangedAction);
        filter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
        _ctx.RegisterReceiver(_receiver, filter);
    }

    public async Task<bool> StartDiscoveryAsync(CancellationToken ct = default)
    {
        if (!await PermissionHelper.EnsureWifiDirectPermissionsAsync(Platform.CurrentActivity))
            return false;

        _pendingDiscover = new();
        _manager.DiscoverPeers(_channel, this);
        return await _pendingDiscover.Task.WaitAsync(ct);
    }

    public Task<bool> ConnectToPeerAsync(string deviceAddress, CancellationToken ct = default)
    {
        _pendingConnect = new();
        var cfg = new WifiP2pConfig { DeviceAddress = deviceAddress };
        _manager.Connect(_channel, cfg, this);
        return _pendingConnect.Task.WaitAsync(ct);
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> buffer, string? peerId = null, CancellationToken ct = default)
    {
        if (_socket is null) return false;
        await _socket.OutputStream.WriteAsync(buffer, ct);
        await _socket.OutputStream.FlushAsync(ct);
        return true;
    }

    public async IAsyncEnumerable<(string PeerId, ReadOnlyMemory<byte> Data)> ReceiveAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        _recvCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var buf = new byte[8192];
        while (!_recvCts.IsCancellationRequested)
        {
            int read = await _socket!.InputStream.ReadAsync(buf, 0, buf.Length, _recvCts.Token);
            if (read <= 0) break;
            yield return ("peer", buf.AsMemory(0, read));
        }
    }

    public Task StopAsync()
    {
        _socket?.Close();
        _manager.RemoveGroup(_channel, null);
        _recvCts?.Cancel();
        return Task.CompletedTask;
    }

    // ---- IActionListener ----
    public void OnSuccess()
    {
        (_pendingDiscover ?? _pendingConnect)?.TrySetResult(true);
    }

    public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
    {
        (_pendingDiscover ?? _pendingConnect)?.TrySetResult(false);
    }

    // ---- IPeerListListener ----
    public void OnPeersAvailable(WifiP2pDeviceList peers)
    {
        // Demonstration: auto-pick first device and connect (production code should show UI)
        var first = peers.DeviceList.FirstOrDefault();
        if (first is not null && _pendingConnect is null)
            _ = ConnectToPeerAsync(first.DeviceAddress);
    }

    // ---- IConnectionInfoListener ----
    public async void OnConnectionInfoAvailable(WifiP2pInfo info)
    {
        if (!info.GroupFormed) return;

        if (info.IsGroupOwner)
        {
            var server = new ServerSocket(8988);
            _socket = await server.AcceptAsync();
        }
        else
        {
            _socket = new Socket();
            await _socket.ConnectAsync(new InetSocketAddress(info.GroupOwnerAddress, 8988), 10_000);
        }

        _pendingConnect?.TrySetResult(_socket?.IsConnected ?? false);
    }
}
