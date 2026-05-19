using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services;

/// <summary>Wraps the low-level <see cref="IBluetoothService"/> to provide a simplified BLE demo surface for DHT22 sensor data and LED control.</summary>
public sealed class BleDemoService : IBleDemoService
{
    private readonly IBluetoothService _ble;
    private bool _ledState;
    private double _lastTemp, _lastHum;

    /// <summary>Raised whenever a new temperature/humidity pair arrives from the BLE peripheral.</summary>
    public event EventHandler<(double temp, double humidity)>? SensorDataReceived;

    /// <summary>Initialises the service and subscribes to the underlying Bluetooth characteristic change events.</summary>
    public BleDemoService(IBluetoothService ble)
    {
        _ble = ble;
        _ble.TemperatureChanged += (_, t) => { _lastTemp = t; FireSensor(); };
        _ble.HumidityChanged += (_, h) => { _lastHum = h; FireSensor(); };
    }

    private void FireSensor() =>
        SensorDataReceived?.Invoke(this, (_lastTemp, _lastHum));

    /// <summary>Connects to the BLE peripheral with the given advertised name.</summary>
    public async Task<bool> ConnectAsync(string deviceName, CancellationToken ct)
    {
        bool ok = await _ble.ConnectAsync(deviceName, ct);
        return ok;
    }

    /// <summary>Disconnects from the currently connected BLE peripheral.</summary>
    public Task DisconnectAsync() => _ble.DisconnectAsync();

    /// <summary>Reads the current temperature and humidity values from the DHT22 sensor over BLE.</summary>
    public async Task<(double temp, double humidity)> ReadDht22Async(CancellationToken cancellationToken)
    {
        var temp = await _ble.ReadTemperatureAsync(cancellationToken);
        var humidity = await _ble.ReadHumidityAsync(cancellationToken);
        return (temp, humidity);
    }

    /// <summary>Toggles the LED state on the peripheral and returns the new state.</summary>
    public async Task<bool> ToggleLedAsync()
    {
        _ledState = !_ledState;
        await _ble.ToggleLedAsync(_ledState, CancellationToken.None);
        return _ledState;
    }

    /// <summary>Starts BLE GATT notifications so sensor data is pushed via <see cref="SensorDataReceived"/>.</summary>
    public Task StartStreamingAsync(CancellationToken ct) =>
        _ble.StartSensorNotificationsAsync(ct);

    /// <summary>Stops BLE GATT notifications.</summary>
    public Task StopStreamingAsync(CancellationToken ct) =>
        _ble.StopSensorNotificationsAsync(ct);
}
