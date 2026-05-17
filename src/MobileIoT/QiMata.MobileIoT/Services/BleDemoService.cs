using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services;

public sealed class BleDemoService : IBleDemoService
{
    private readonly IBluetoothService _ble;
    private bool _ledState;
    private double _lastTemp, _lastHum;

    public event EventHandler<(double temp, double humidity)>? SensorDataReceived;

    public BleDemoService(IBluetoothService ble)
    {
        _ble = ble;
        _ble.TemperatureChanged += (_, t) => { _lastTemp = t; FireSensor(); };
        _ble.HumidityChanged += (_, h) => { _lastHum = h; FireSensor(); };
    }

    private void FireSensor() =>
        SensorDataReceived?.Invoke(this, (_lastTemp, _lastHum));

    public async Task<bool> ConnectAsync(string deviceName, CancellationToken ct)
    {
        bool ok = await _ble.ConnectAsync(deviceName, ct);
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

    public Task StartStreamingAsync(CancellationToken ct) =>
        _ble.StartSensorNotificationsAsync(ct);

    public Task StopStreamingAsync(CancellationToken ct) =>
        _ble.StopSensorNotificationsAsync(ct);
}
