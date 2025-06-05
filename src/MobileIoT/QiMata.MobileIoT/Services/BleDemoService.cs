using QiMata.MobileIoT.Services.I;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services;

public sealed class BleDemoService : IBleDemoService
{
    private readonly IBluetoothService _ble;
    private bool _ledState;

    public BleDemoService(IBluetoothService ble) => _ble = ble;

    public async Task<bool> ConnectAsync(string deviceName, CancellationToken ct)
    {
        bool ok = await _ble.ConnectAsync(deviceName, ct);
        //if (ok)
        //    await _ble.StartSensorNotificationsAsync(ct);
        return ok;
    }

    public Task DisconnectAsync() => _ble.DisconnectAsync();

    public async Task<(double temp, double humidity)> ReadDht22Async(CancellationToken cancellationToken)
    {
        var temp = await _ble.ReadTemperatureAsync(cancellationToken);
        var humidity = await _ble.ReadHumidityAsync(cancellationToken);
        return (temp, humidity);
    }

    public async Task<bool> ToggleLedAsync()
    {
        _ledState = !_ledState;
        await _ble.ToggleLedAsync(_ledState, CancellationToken.None);
        return _ledState;
    }
}
