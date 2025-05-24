namespace QiMata.MobileIoT.Services.I;

/// <summary>
/// Advertisement details from a BLE beacon.
/// </summary>
public record BeaconAdvertisement(
    string DeviceId,
    string? Name,
    byte[] Data,
    int Rssi);

public interface IBeaconScanner
{
    event EventHandler<BeaconAdvertisement> AdvertisementReceived;
    void StartScanning();
    void StopScanning();
    bool IsScanning { get; }
}
