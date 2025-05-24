using Android.Nfc;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT;

partial class MainActivity
{
    protected override void OnNewIntent(Android.Content.Intent intent)
    {
        base.OnNewIntent(intent);

        // Give the P2P service first crack at the intent
        (DependencyService.Get<INfcP2PService>() as NfcP2PService_Android)
            ?.HandleIntent(intent);

        // Still let Plugin.NFC process tag scans, if used
        Plugin.NFC.CrossNFC.OnNewIntent(intent);
    }
}
