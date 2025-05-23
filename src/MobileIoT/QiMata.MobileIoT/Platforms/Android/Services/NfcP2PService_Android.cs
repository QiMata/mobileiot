using QiMata.MobileIoT.Services.I;

using Android.Nfc;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform;
using QiMata.MobileIoT.Services;
[assembly: Microsoft.Maui.Controls.Dependency(typeof(QiMata.MobileIoT.Platforms.Android.NfcP2PService_Android))]

namespace QiMata.MobileIoT.Platforms.Android;
public class NfcP2PService_Android : Java.Lang.Object,
                                     INfcP2PService,
                                     NfcAdapter.ICreateNdefMessageCallback,
                                     NfcAdapter.IOnNdefPushCompleteCallback
{
    NfcAdapter? _adapter;

    public void StartP2P()
    {
        var activity = Platform.CurrentActivity ?? MauiApplication.Current.GetActivity();
        _adapter = NfcAdapter.GetDefaultAdapter(activity);
        if (_adapter == null || activity == null) return;     // device has no NFC

        _adapter.SetNdefPushMessageCallback(this, activity);
        _adapter.SetOnNdefPushCompleteCallback(this, activity);
    }

    public void StopP2P()
    {
        var activity = Platform.CurrentActivity ?? MauiApplication.Current.GetActivity();
        if (_adapter == null || activity == null)
        {
            return; 
        }
        _adapter?.SetNdefPushMessageCallback(null, activity);
        _adapter?.SetOnNdefPushCompleteCallback(null, activity);
    }

    // Builds the message sent to the peer
    public NdefMessage CreateNdefMessage(NfcEvent e)
    {
        const string payloadText = "Hello from .NET MAUI";
        var mimeType = System.Text.Encoding.ASCII.GetBytes("application/vnd.yourapp.p2p");
        var payload   = System.Text.Encoding.UTF8.GetBytes(payloadText);

        var record = new NdefRecord(NdefRecord.TnfMimeMedia, mimeType, Array.Empty<byte>(), payload);
        return new NdefMessage(new[] { record });
    }

    // Optional confirmation callback
    public void OnNdefPushComplete(NfcEvent e)
    {
        System.Diagnostics.Debug.WriteLine("NDEF push completed.");
    }
}
