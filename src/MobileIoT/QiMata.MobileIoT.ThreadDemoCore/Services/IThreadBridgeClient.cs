using QiMata.MobileIoT.ThreadDemoCore.Models;

namespace QiMata.MobileIoT.ThreadDemoCore.Services;

public interface IThreadBridgeClient
{
    Task<ThreadStatusSnapshot> GetStatusAsync(string baseUrl, int timeoutMs, CancellationToken ct = default);
    Task<ThreadPingResult> SendPingAsync(string baseUrl, string target, string payload, int timeoutMs, CancellationToken ct = default);
}
