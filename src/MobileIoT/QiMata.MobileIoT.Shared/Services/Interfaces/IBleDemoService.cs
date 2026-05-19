using System.Threading;

namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Provides BLE-based access to the Pi DHT22 sensor and onboard LED for the demo experience.</summary>
public interface IBleDemoService
{
    /// <summary>Connects to the BLE peripheral with the given advertised device name.</summary>
    Task<bool> ConnectAsync(string deviceName, CancellationToken ct);

    /// <summary>Disconnects from the currently connected BLE peripheral.</summary>
    Task DisconnectAsync();

    /// <summary>Reads the current temperature and humidity values from the DHT22 sensor over BLE.</summary>
    Task<(double temp, double humidity)> ReadDht22Async(CancellationToken ct);

    /// <summary>Toggles the LED on the peripheral and returns the new LED state.</summary>
    Task<bool> ToggleLedAsync();

    /// <summary>Raised whenever a new temperature/humidity pair is received from the peripheral.</summary>
    event EventHandler<(double temp, double humidity)>? SensorDataReceived;

    /// <summary>Enables GATT notifications so sensor readings are pushed continuously via <see cref="SensorDataReceived"/>.</summary>
    Task StartStreamingAsync(CancellationToken ct);

    /// <summary>Disables GATT notifications, stopping the continuous sensor stream.</summary>
    Task StopStreamingAsync(CancellationToken ct);
}
