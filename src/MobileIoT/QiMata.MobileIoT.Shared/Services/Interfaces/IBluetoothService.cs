using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Low-level BLE client for the Pi DHT22/LED peripheral, exposing characteristic reads, LED control, and GATT notifications.</summary>
public interface IBluetoothService : IAsyncDisposable
{
    /// <summary>Scans for and connects to the BLE peripheral with the given advertised name.</summary>
    Task<bool> ConnectAsync(string advertisedName, CancellationToken ct);

    /// <summary>Reads the current temperature value from the GATT temperature characteristic.</summary>
    Task<float> ReadTemperatureAsync(CancellationToken ct);

    /// <summary>Reads the current humidity value from the GATT humidity characteristic.</summary>
    Task<float> ReadHumidityAsync(CancellationToken ct);

    /// <summary>Writes the LED on/off command to the GATT LED characteristic.</summary>
    Task ToggleLedAsync(bool on, CancellationToken ct);

    /// <summary>Disconnects from the currently connected BLE peripheral.</summary>
    Task DisconnectAsync();

    /// <summary>Raised when the peripheral notifies a new temperature reading.</summary>
    event EventHandler<float>? TemperatureChanged;

    /// <summary>Raised when the peripheral notifies a new humidity reading.</summary>
    event EventHandler<float>? HumidityChanged;

    /// <summary>Subscribes to GATT notifications for both temperature and humidity characteristics.</summary>
    Task StartSensorNotificationsAsync(CancellationToken ct);

    /// <summary>Unsubscribes from GATT notifications for both temperature and humidity characteristics.</summary>
    Task StopSensorNotificationsAsync(CancellationToken ct);
}
