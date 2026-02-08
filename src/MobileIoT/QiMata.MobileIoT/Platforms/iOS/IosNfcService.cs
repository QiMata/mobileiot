#if IOS
using CoreFoundation;
using CoreNFC;
using Foundation;
using System.Linq;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT;

public class IosNfcService : NSObject, INfcService, INFCNdefReaderSessionDelegate
{
    NFCNdefReaderSession? session;
    TaskCompletionSource? writerTcs;
    string? _pendingWriteText;

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
        _pendingWriteText = text;
        writerTcs = new TaskCompletionSource();
        session   = new NFCNdefReaderSession(this, null, false);
        session?.BeginSession();
        return writerTcs.Task;
    }

    public event EventHandler<string>? MessageReceived;

    public void DidDetect(NFCNdefReaderSession s, NFCNdefMessage[] messages)
    {
        if (_pendingWriteText is not null)
        {
            // We're in write mode — need to connect to tag and write
            // DidDetect with messages means the session detected NDEF tags;
            // for writing we need the tag-based overload via DidDetectTags.
            // Fall through to read handling if we get here unexpectedly.
        }

        foreach (var msg in messages)
        {
            foreach (var rec in msg.Records)
            {
                var payload = rec.Payload.Skip(1).ToArray();
                var text = System.Text.Encoding.UTF8.GetString(payload);
                MessageReceived?.Invoke(this, text);
            }
        }
    }

    [Export("readerSession:didDetectTags:")]
    public void DidDetectTags(NFCNdefReaderSession s, INFCNdefTag[] tags)
    {
        if (_pendingWriteText is null || tags.Length == 0)
            return;

        var tag = tags[0];
        var textToWrite = _pendingWriteText;
        _pendingWriteText = null;

        s.ConnectToTag(tag, (connectError) =>
        {
            if (connectError is not null)
            {
                s.InvalidateSession("Failed to connect to tag.");
                writerTcs?.TrySetException(new InvalidOperationException(connectError.LocalizedDescription));
                return;
            }

            tag.QueryNdefStatus((status, capacity, queryError) =>
            {
                if (queryError is not null || status == NFCNdefStatus.NotSupported)
                {
                    s.InvalidateSession("Tag is not NDEF compatible.");
                    writerTcs?.TrySetException(new InvalidOperationException("Tag is not NDEF compatible."));
                    return;
                }

                if (status == NFCNdefStatus.ReadOnly)
                {
                    s.InvalidateSession("Tag is read-only.");
                    writerTcs?.TrySetException(new InvalidOperationException("Tag is read-only."));
                    return;
                }

                var textBytes = System.Text.Encoding.UTF8.GetBytes(textToWrite);
                var payload = new byte[1 + textBytes.Length];
                payload[0] = 0x00; // UTF-8, language length = 0
                Buffer.BlockCopy(textBytes, 0, payload, 1, textBytes.Length);

                var record = new NFCNdefPayload(
                    NFCTypeNameFormat.NfcWellKnown,
                    NSData.FromArray(new byte[] { 0x54 }), // 'T' for Text
                    new NSData(),
                    NSData.FromArray(payload));

                var message = new NFCNdefMessage(new[] { record });

                tag.WriteNdef(message, (writeError) =>
                {
                    if (writeError is not null)
                    {
                        s.InvalidateSession("Write failed.");
                        writerTcs?.TrySetException(new InvalidOperationException(writeError.LocalizedDescription));
                    }
                    else
                    {
                        s.InvalidateSession("Write successful.");
                        writerTcs?.TrySetResult();
                    }
                });
            });
        });
    }

    public void DidInvalidate(NFCNdefReaderSession s, NSError error)
    {
        // If the session was invalidated by user cancel or error and writer is still pending
        if (writerTcs is not null && !writerTcs.Task.IsCompleted)
        {
            if (error.Code == (long)NFCReaderError.ReaderSessionInvalidationErrorUserCanceled)
                writerTcs.TrySetCanceled();
            else if (error.Code != (long)NFCReaderError.ReaderSessionInvalidationErrorFirstNdefTagRead)
                writerTcs.TrySetException(new InvalidOperationException(error.LocalizedDescription));
        }
    }
}
#endif
