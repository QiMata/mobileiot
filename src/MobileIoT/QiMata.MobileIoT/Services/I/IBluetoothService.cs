using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services.I;

public interface IBluetoothService : IAsyncDisposable
{
    Task<bool> ConnectAsync(string advertisedName, CancellationToken ct);

    Task<float> ReadTemperatureAsync(CancellationToken ct);

    Task<float> ReadHumidityAsync(CancellationToken ct);
    Task ToggleLedAsync(bool on, CancellationToken ct);
    Task DisconnectAsync();
}
