using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services.I;

public interface IBluetoothService : IAsyncDisposable
{
    event EventHandler<double> TemperatureUpdatedC;
    event EventHandler<double> HumidityUpdatedPercent;

    Task<bool> ConnectAsync(string advertisedName, CancellationToken ct);
    Task StartSensorNotificationsAsync(CancellationToken ct);
    Task ToggleLedAsync(bool on, CancellationToken ct);
    Task DisconnectAsync();
}
