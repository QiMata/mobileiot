using System.Threading;

namespace QiMata.MobileIoT.Services.Interfaces;

public interface IBleDemoService
{
    Task<bool> ConnectAsync(string deviceName, CancellationToken ct);
    Task DisconnectAsync();
    Task<(double temp, double humidity)> ReadDht22Async(CancellationToken ct);
    Task<bool> ToggleLedAsync();
}
