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

    public async Task<(double temp, double humidity)> ReadDht22Async()
    {
        var tcs = new TaskCompletionSource<(double, double)>();

        EventHandler<double>? tempHandler = null;
        EventHandler<double>? humHandler = null;

        double? temp = null;
        double? hum = null;

        tempHandler = (_, v) =>
        {
            temp = v;
            if (temp.HasValue && hum.HasValue)
                tcs.TrySetResult((temp.Value, hum.Value));
        };

        humHandler = (_, v) =>
        {
            hum = v;
            if (temp.HasValue && hum.HasValue)
                tcs.TrySetResult((temp.Value, hum.Value));
        };

        _ble.TemperatureUpdatedC += tempHandler;
        _ble.HumidityUpdatedPercent += humHandler;

        await _ble.StartSensorNotificationsAsync(CancellationToken.None);

        var reading = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        _ble.TemperatureUpdatedC -= tempHandler;
        _ble.HumidityUpdatedPercent -= humHandler;

        return reading;
    }

    public async Task<bool> ToggleLedAsync()
    {
        _ledState = !_ledState;
        await _ble.ToggleLedAsync(_ledState, CancellationToken.None);
        return _ledState;
    }
}
