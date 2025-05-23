namespace QiMata.MobileIoT.Services.I;

public record BeaconAdvertisement(byte[] Data, int Rssi);

public interface IBeaconScanner
{
    event EventHandler<BeaconAdvertisement> AdvertisementReceived;
    void StartScanning();
    void StopScanning();
    bool IsScanning { get; }
}
