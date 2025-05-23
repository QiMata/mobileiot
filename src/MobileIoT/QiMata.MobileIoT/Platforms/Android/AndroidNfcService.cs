#if ANDROID
using Android.Content;
using Android.Nfc;
using Microsoft.Maui.Controls;
using Plugin.NFC;
using System.Linq;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT;

public class AndroidNfcService : Java.Lang.Object, INfcService
{
    public bool IsAvailable => CrossNFC.Current.IsAvailable;
    public bool IsEnabled   => CrossNFC.Current.IsEnabled;

    public Task StartListeningAsync()
    {
        CrossNFC.Current.OnMessageReceived += OnMessageReceived;
        CrossNFC.Current.StartListening();
        return Task.CompletedTask;
    }

    public Task StopListeningAsync()
    {
        CrossNFC.Current.OnMessageReceived -= OnMessageReceived;
        CrossNFC.Current.StopListening();
        return Task.CompletedTask;
    }

    public Task WriteTextAsync(string text)
    {
        var record = new NFCNdefRecord
        {
            TypeFormat = NFCNdefTypeFormat.Mime,
            MimeType = "application/com.companyname.yourapp",
            Payload = NFCUtils.EncodeToByteArray(text)
        };


        CrossNFC.Current.OnTagDiscovered += (tagInfo, format) =>
        {
            if (tagInfo.IsWritable)
            {
                tagInfo.Records = [record];
                CrossNFC.Current.PublishMessage(tagInfo);
            }
        };

        CrossNFC.Current.StartPublishing();
        return Task.CompletedTask;
    }

    public event EventHandler<string>? MessageReceived;

    void OnMessageReceived(ITagInfo tagInfo)
    {
        if (!tagInfo.IsSupported) return;

        var first = tagInfo.Records?.FirstOrDefault();
        if (first?.TypeFormat == NFCNdefTypeFormat.WellKnown)
        {
            var payloadText = first.Message;
            MessageReceived?.Invoke(this, payloadText);
        }
    }
}
#endif
