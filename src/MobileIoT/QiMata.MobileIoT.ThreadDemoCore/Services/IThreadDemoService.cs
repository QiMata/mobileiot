using QiMata.MobileIoT.ThreadDemoCore.Models;

namespace QiMata.MobileIoT.ThreadDemoCore.Services;

public interface IThreadDemoService
{
    Task<ThreadStatusSnapshot> GetStatusAsync(ThreadConnectionSettings settings, CancellationToken ct = default);
    Task<ThreadPingResult> SendPingAsync(ThreadConnectionSettings settings, string payload, CancellationToken ct = default);
}
