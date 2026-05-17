using QiMata.MobileIoT.Shared.Services;
using Android.Content;
using Android.Net.Wifi.P2p;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
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
using Application = Android.App.Application;

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
    readonly IAppLogger _logger;
    bool _receiverRegistered;
    TaskCompletionSource<bool>? _pendingDiscover;
    TaskCompletionSource<bool>? _pendingConnect;
    TaskCompletionSource<bool>? _pendingAction;
    Socket? _socket;
    ServerSocket? _serverSocket;
    CancellationTokenSource? _recvCts;

    public WifiDirectService(IAppLogger logger)
    {
        _logger = logger;
        _manager = (WifiP2pManager)_ctx.GetSystemService(Context.WifiP2pService)!;
        _channel = _manager.Initialize(_ctx, Looper.MainLooper, null);
        _receiver = new WifiP2pBroadcastReceiver(_manager, _channel, this, this);
        var filter = new IntentFilter(WifiP2pManager.WifiP2pPeersChangedAction);
        filter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
        _ctx.RegisterReceiver(_receiver, filter);
        _receiverRegistered = true;
    }

    public async Task<bool> StartDiscoveryAsync(CancellationToken ct = default)
    {
        if (!await PermissionHelper.EnsureWifiDirectPermissionsAsync(Platform.CurrentActivity))
            return false;

        _pendingDiscover = new();
        _pendingAction = _pendingDiscover;
        _manager.DiscoverPeers(_channel, this);
        return await _pendingDiscover.Task.WaitAsync(ct);
    }

    public Task<bool> ConnectToPeerAsync(string deviceAddress, CancellationToken ct = default)
    {
        _pendingConnect = new();
        _pendingAction = _pendingConnect;
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
        _serverSocket?.Close();
        _serverSocket = null;
        _manager.RemoveGroup(_channel, null);
        _recvCts?.Cancel();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _receiverRegistered)
        {
            try { _ctx.UnregisterReceiver(_receiver); }
            catch (Exception ex) { _logger.Warn("Failed to unregister WifiP2p broadcast receiver", ex); }
            _receiverRegistered = false;
        }
        base.Dispose(disposing);
    }

    public void OnSuccess()
    {
        _pendingAction?.TrySetResult(true);
        _pendingAction = null;
    }

    public void OnFailure([GeneratedEnum] WifiP2pFailureReason reason)
    {
        _pendingAction?.TrySetResult(false);
        _pendingAction = null;
    }

    public void OnPeersAvailable(WifiP2pDeviceList peers)
    {
        var first = peers.DeviceList.FirstOrDefault();
        if (first is not null && _pendingConnect is null)
            _ = ConnectToPeerAsync(first.DeviceAddress);
    }

    public async void OnConnectionInfoAvailable(WifiP2pInfo info)
    {
        if (!info.GroupFormed) return;

        try
        {
            if (info.IsGroupOwner)
            {
                _serverSocket = new ServerSocket(TransportConstants.WifiDirectPort);
                _serverSocket.SoTimeout = TransportConstants.WifiDirectConnectTimeoutSeconds * 1000;
                _socket = await _serverSocket.AcceptAsync();
                _serverSocket.Close();
                _serverSocket = null;
            }
            else
            {
                _socket = new Socket();
                await _socket.ConnectAsync(
                    new InetSocketAddress(info.GroupOwnerAddress, TransportConstants.WifiDirectPort),
                    TransportConstants.WifiDirectConnectTimeoutSeconds * 1000);
            }

            _pendingConnect?.TrySetResult(_socket?.IsConnected ?? false);
        }
        catch (Java.IO.IOException ex)
        {
            _logger.Warn("WifiDirect connection failed", ex);
            _pendingConnect?.TrySetResult(false);
        }
    }
}
