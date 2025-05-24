using Android.Content;
using Android.Hardware.Usb;
using Microsoft.Maui.Controls;

namespace QiMata.MobileIoT.Platforms.Android;

[BroadcastReceiver(Exported = true)]
[IntentFilter(new[] { "USB_PERMISSION" })]
public class UsbPermissionReceiver : BroadcastReceiver
{
    public override void OnReceive(Context context, Intent intent) =>
        MessagingCenter.Send<object, bool>(this, "UsbPermissionResult",
            intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false));
}
