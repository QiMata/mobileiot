using QiMata.MobileIoT.Shared.Thread.Models;

namespace QiMata.MobileIoT.Shared.Thread.Services;

public interface IThreadBridgeClient
{
    Task<ThreadStatusSnapshot> GetStatusAsync(string baseUrl, int timeoutMs, CancellationToken ct = default);
    Task<ThreadPingResult> SendPingAsync(string baseUrl, string target, string payload, int timeoutMs, CancellationToken ct = default);
}
