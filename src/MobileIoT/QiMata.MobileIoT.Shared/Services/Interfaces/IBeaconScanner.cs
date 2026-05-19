namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>
/// Advertisement details from a BLE beacon.
/// </summary>
public record BeaconAdvertisement(
    string DeviceId,
    string? Name,
    byte[] Data,
    int Rssi);

/// <summary>Scans for BLE beacon advertisements and raises an event for each one received.</summary>
public interface IBeaconScanner
{
    /// <summary>Raised each time a BLE advertisement packet is received.</summary>
    event EventHandler<BeaconAdvertisement> AdvertisementReceived;

    /// <summary>Starts an active BLE advertisement scan.</summary>
    void StartScanning();

    /// <summary>Stops the active BLE advertisement scan.</summary>
    void StopScanning();

    /// <summary>Indicates whether a scan is currently active.</summary>
    bool IsScanning { get; }
}
