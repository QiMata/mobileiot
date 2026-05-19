namespace QiMata.MobileIoT.Shared.Services;

/// <summary>Provides peer-to-peer transport (Wi-Fi Direct / Multipeer) for discovering, connecting to, and exchanging data with nearby peers.</summary>
public interface IP2PService
{
    /// <summary>Starts peer discovery and returns true if the discovery session was initiated successfully.</summary>
    Task<bool> StartDiscoveryAsync(CancellationToken ct = default);

    /// <summary>Connects to the peer identified by <paramref name="peerId"/> and returns true on success.</summary>
    Task<bool> ConnectToPeerAsync(string peerId, CancellationToken ct = default);

    /// <summary>Sends a byte buffer to the specified peer, or broadcasts to all connected peers when <paramref name="peerId"/> is null.</summary>
    Task<bool> SendAsync(ReadOnlyMemory<byte> buffer, string? peerId = null, CancellationToken ct = default);

    /// <summary>Returns an async sequence of incoming data frames, each tagged with the sending peer's identifier.</summary>
    IAsyncEnumerable<(string PeerId, ReadOnlyMemory<byte> Data)> ReceiveAsync(CancellationToken ct = default);

    /// <summary>Stops discovery and disconnects from all peers.</summary>
    Task StopAsync();
}
