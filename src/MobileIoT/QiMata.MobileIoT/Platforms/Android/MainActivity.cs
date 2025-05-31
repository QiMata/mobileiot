using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Nfc;
using Android.Hardware.Usb;
using Android.OS;
using Plugin.NFC;

namespace QiMata.MobileIoT;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
          ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
          Exported = true)]
[IntentFilter(
    new[] { NfcAdapter.ActionNdefDiscovered },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/plain")]
[IntentFilter(
    new[] { NfcAdapter.ActionNdefDiscovered },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "application/vnd.yourapp.p2p")]
[IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached })]
[MetaData("android.hardware.usb.action.USB_DEVICE_ATTACHED", Resource = "@xml/device_filter")]
public partial class MainActivity : MauiAppCompatActivity
{
    internal static event Action<int, string[], Permission[]>? PermissionsResultReceived;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CrossNFC.Init(this);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        PermissionsResultReceived?.Invoke(requestCode, permissions, grantResults);
    }

}
