namespace QiMata.MobileIoT.Services.Interfaces;

/// <summary>
/// Phone-as-BLE-peripheral surface used by the TestHarness `ble-peripheral`
/// scenario. Android implementation uses BluetoothLeAdvertiser +
/// BluetoothGattServer; iOS implementation wraps CBPeripheralManager.
/// </summary>
public interface IBleAdvertiseService
{
    bool IsAvailable { get; }
    Task StartAsync(Guid serviceUuid, byte[] payload, string deviceName, CancellationToken cancellationToken);
    Task StopAsync();
    event EventHandler<byte[]>? CharacteristicWritten;
    event EventHandler? CharacteristicRead;
}
