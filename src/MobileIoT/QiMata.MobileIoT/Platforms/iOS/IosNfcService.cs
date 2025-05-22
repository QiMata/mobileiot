#if IOS
using CoreFoundation;
using CoreNFC;
using Foundation;
using Microsoft.Maui.Controls;
using System.Linq;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT;

public class IosNfcService : NSObject, INfcService, INFCNdefReaderSessionDelegate, INFCNdefTag
{
    NFCNdefReaderSession? session;
    TaskCompletionSource? writerTcs;

    public bool IsAvailable => NFCNdefReaderSession.ReadingAvailable;
    public bool IsEnabled   => IsAvailable;

    public Task StartListeningAsync()
    {
        session = new NFCNdefReaderSession(this, null, true);
        session?.BeginSession();
        return Task.CompletedTask;
    }

    public Task StopListeningAsync()
    {
        session?.InvalidateSession();
        return Task.CompletedTask;
    }

    public Task WriteTextAsync(string text)
    {
        writerTcs = new TaskCompletionSource();
        session   = new NFCNdefReaderSession(this, null, false);
        session?.BeginSession();
        return writerTcs.Task;
    }

    public void DidDetect(NFCNdefReaderSession s, NFCNdefMessage[] messages)
    {
        foreach (var msg in messages)
        {
            foreach (var rec in msg.Records)
            {
                var payload = rec.Payload.Skip(1).ToArray();
                var text = System.Text.Encoding.UTF8.GetString(payload);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current.MainPage.DisplayAlert("Tag Read", text, "OK");
                });
            }
        }
    }

    public void DidInvalidate(NFCNdefReaderSession s, NSError error) { }

    public void DidDetectTags(NFCTagReaderSession s, INFCTag[] tags) { }
}
#endif
