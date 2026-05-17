using QiMata.MobileIoT.Shared.Thread.Models;

namespace QiMata.MobileIoT.Shared.Thread.Services;

public interface IThreadDemoService
{
    Task<ThreadStatusSnapshot> GetStatusAsync(ThreadConnectionSettings settings, CancellationToken ct = default);
    Task<ThreadPingResult> SendPingAsync(ThreadConnectionSettings settings, string payload, CancellationToken ct = default);
}
