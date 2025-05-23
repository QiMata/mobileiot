using Android.Nfc;
using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT;

partial class MainActivity
{
    protected override void OnNewIntent(Android.Content.Intent intent)
    {
        base.OnNewIntent(intent);

        if (NfcAdapter.ActionNdefDiscovered.Equals(intent.Action))
        {
            var raw = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
            if (raw?.Length > 0)
            {
                var msg = (NdefMessage)raw[0]!;
                var record = msg.GetRecords().FirstOrDefault();
                var text = System.Text.Encoding.UTF8.GetString(record!.GetPayload());
                MainThread.BeginInvokeOnMainThread(() =>
                    Shell.Current.DisplayAlert("Received", text, "OK"));
            }
        }

        Plugin.NFC.CrossNFC.OnNewIntent(intent);
    }
}
