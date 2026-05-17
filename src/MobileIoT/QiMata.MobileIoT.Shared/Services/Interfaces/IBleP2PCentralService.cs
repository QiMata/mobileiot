namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>
/// Phone-as-BLE-central surface used by the TestHarness `ble-p2p-central`
/// scenario. Scans by advertised name, connects, writes the payload to the
/// service's first characteristic, optionally reads it back, returns the
/// response bytes (or `null` on timeout).
/// </summary>
public interface IBleP2PCentralService
{
    Task<byte[]?> ConnectAndExchangeAsync(
        string deviceName,
        Guid serviceUuid,
        byte[] payload,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
