#if ANDROID
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Runtime;
using Java.Util;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class BeaconScanner_Android : Java.Lang.Object, IBeaconScanner
{
    readonly BluetoothLeScanner _scanner;
    readonly ScanCallbackImpl   _callback;
    public event EventHandler<BeaconAdvertisement>? AdvertisementReceived;
    public bool IsScanning { get; private set; }

    public BeaconScanner_Android()
    {
        var adapter = BluetoothAdapter.DefaultAdapter
                     ?? throw new InvalidOperationException("Bluetooth unavailable");
        _scanner  = adapter.BluetoothLeScanner!;
        _callback = new ScanCallbackImpl(OnAdv);
    }

    public void StartScanning()
    {
        if (IsScanning) return;
        _scanner.StartScan(_callback);
        IsScanning = true;
    }

    public void StopScanning()
    {
        if (!IsScanning) return;
        _scanner.StopScan(_callback);
        IsScanning = false;
    }

    void OnAdv(ScanResult result)
    {
        var rec  = result?.ScanRecord;
        var data = rec?.GetBytes();
        if (data?.Length > 0)
            AdvertisementReceived?.Invoke(this,
                new BeaconAdvertisement(
                    result.Device.Address,
                    result.Device.Name,
                    data,
                    result.Rssi));
    }

    sealed class ScanCallbackImpl : ScanCallback
    {
        readonly Action<ScanResult> _on;
        public ScanCallbackImpl(Action<ScanResult> on) => _on = on;
        public override void OnScanResult(ScanCallbackType t, ScanResult r) => _on(r);
    }
}
#endif
