using Android.App;
using Android.Content;
using Android.Hardware.Usb;

namespace QiMata.MobileIoT.Platforms.Android;

[BroadcastReceiver(Exported = true)]
[IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached, ACTION_USB_PERMISSION })]
public sealed class UsbPermissionBroadcastReceiver : BroadcastReceiver
{
    public const string ACTION_USB_PERMISSION = "com.qimata.mobileiot.USB_PERMISSION";

    public override void OnReceive(Context context, Intent intent)
    {
        if (intent.Action != ACTION_USB_PERMISSION) return;

        var granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
        var device  = (UsbDevice?)intent.GetParcelableExtra(UsbManager.ExtraDevice);
        if (granted && device != null)
        {
            UsbSerialDeviceService.UnblockPermission(device.DeviceId);
        }
    }
}
