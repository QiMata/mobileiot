namespace QiMata.MobileIoT.Services;

public interface IP2PService
{
    Task<bool> StartDiscoveryAsync(CancellationToken ct = default);
    Task<bool> ConnectToPeerAsync(string peerId, CancellationToken ct = default);
    Task<bool> SendAsync(ReadOnlyMemory<byte> buffer, string? peerId = null, CancellationToken ct = default);
    IAsyncEnumerable<(string PeerId, ReadOnlyMemory<byte> Data)> ReceiveAsync(CancellationToken ct = default);
    Task StopAsync();
}
