namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>
/// Phone-as-BLE-peripheral surface used by the TestHarness `ble-peripheral`
/// scenario. Android implementation uses BluetoothLeAdvertiser +
/// BluetoothGattServer; iOS implementation wraps CBPeripheralManager.
/// </summary>
public interface IBleAdvertiseService
{
    /// <summary>Indicates whether BLE peripheral advertising is supported on this device.</summary>
    bool IsAvailable { get; }

    /// <summary>Starts advertising the device as a BLE peripheral with the given service UUID, initial payload, and local name.</summary>
    Task StartAsync(Guid serviceUuid, byte[] payload, string deviceName, CancellationToken cancellationToken);

    /// <summary>Stops advertising and shuts down the GATT server.</summary>
    Task StopAsync();

    /// <summary>Raised when a central device writes a value to the exposed GATT characteristic.</summary>
    event EventHandler<byte[]>? CharacteristicWritten;

    /// <summary>Raised when a central device reads from the exposed GATT characteristic.</summary>
    event EventHandler? CharacteristicRead;
}
