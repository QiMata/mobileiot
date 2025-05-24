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
    string _textToSend = string.Empty;

    public event EventHandler<NfcMessageEventArgs>? MessageReceived;

    public void StartP2P(string textToSend)
    {
        _textToSend = textToSend ?? string.Empty;   // save for CreateNdefMessage
        var activity = Platform.CurrentActivity ?? MauiApplication.Current.GetActivity();
        _adapter = NfcAdapter.GetDefaultAdapter(activity);
        if (_adapter == null) return;

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

    // Build the NDEF to beam, using the caller-supplied text
    public NdefMessage CreateNdefMessage(NfcEvent e)
    {
        var mimeType = System.Text.Encoding.ASCII.GetBytes("application/vnd.yourapp.p2p");
        var payload  = System.Text.Encoding.UTF8.GetBytes(_textToSend);
        var record   = new NdefRecord(NdefRecord.TnfMimeMedia, mimeType, Array.Empty<byte>(), payload);
        return new NdefMessage(new[] { record });
    }

    public void OnNdefPushComplete(NfcEvent e) =>
        System.Diagnostics.Debug.WriteLine("NDEF push completed.");

    // Called from MainActivity to parse incoming intents
    internal void HandleIntent(Android.Content.Intent intent)
    {
        if (!NfcAdapter.ActionNdefDiscovered.Equals(intent.Action)) return;

        var raw = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
        if (raw?.Length > 0)
        {
            var msg    = (NdefMessage)raw[0]!;
            var record = msg.GetRecords().FirstOrDefault();
            if (record == null) return;

            var mimeType = System.Text.Encoding.ASCII.GetString(record.GetType());
            var text     = System.Text.Encoding.UTF8.GetString(record.GetPayload());

            MessageReceived?.Invoke(this,
                new NfcMessageEventArgs(mimeType, text, record.GetPayload()));
        }
    }
}
