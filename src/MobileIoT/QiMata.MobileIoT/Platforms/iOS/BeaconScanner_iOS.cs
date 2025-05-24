#if IOS
using CoreBluetooth;
using CoreFoundation;
using Foundation;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Platforms.iOS;

public sealed class BeaconScanner_iOS : NSObject, IBeaconScanner, ICBCentralManagerDelegate
{
    readonly CBCentralManager _central;
    public event EventHandler<BeaconAdvertisement>? AdvertisementReceived;
    public bool IsScanning { get; private set; }

    public BeaconScanner_iOS() =>
        _central = new CBCentralManager(this, DispatchQueue.MainQueue);

    public void StartScanning()
    {
        if (IsScanning || _central.State != CBManagerState.PoweredOn) return;
        var opts = new PeripheralScanningOptions { AllowDuplicatesKey = true };
        _central.ScanForPeripherals(peripheralUuids: null, options: opts);
        IsScanning = true;
    }

    public void StopScanning()
    {
        if (!IsScanning) return;
        _central.StopScan();
        IsScanning = false;
    }

    [Export("centralManagerDidUpdateState:")]
    public void UpdatedState(CBCentralManager central)
    {
        if (central.State == CBManagerState.PoweredOn) StartScanning();
    }

    [Export("centralManager:didDiscoverPeripheral:advertisementData:RSSI:")]
    public void DiscoveredPeripheral(CBCentralManager c, CBPeripheral p,
                                     NSDictionary ad, NSNumber rssi)
    {
        if (ad[CBAdvertisement.DataManufacturerDataKey] is NSData d && d.Length > 0)
            AdvertisementReceived?.Invoke(this,
                new BeaconAdvertisement(
                    p.Identifier.AsString(),
                    p.Name,
                    d.ToArray(),
                    rssi.Int32Value));
    }
}
#endif
