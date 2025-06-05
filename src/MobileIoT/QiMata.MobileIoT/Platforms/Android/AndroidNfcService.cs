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

    static byte[] BuildTextPayload(string txt)
    {
        // NDEF Text record payload: [status byte][text] (no language code)
        var textBytes = System.Text.Encoding.UTF8.GetBytes(txt);

        var payload = new byte[1 + textBytes.Length];
        payload[0] = 0x00; // UTF-8 (bit 7 = 0) + language length = 0
        Buffer.BlockCopy(textBytes, 0, payload, 1, textBytes.Length);

        return payload;
    }


    bool _startedPublishing = false;

    public async Task WriteTextAsync(string text)
    {
        var record = new NFCNdefRecord
        {
            TypeFormat = NFCNdefTypeFormat.WellKnown,
            Payload = BuildTextPayload(text)
        };

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        

        void Cleanup()
        {
            if (_startedPublishing)
                CrossNFC.Current.StopPublishing();

            CrossNFC.Current.OnTagDiscovered -= TagHandler;
        }

        void TagHandler(ITagInfo tagInfo, bool _)
        {
            try
            {
                if (tagInfo?.IsWritable == true)
                {
                    tagInfo.Records = new[] { record };
                    CrossNFC.Current.PublishMessage(tagInfo);
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetException(new InvalidOperationException("Tag is not writable."));
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                Cleanup();
            }
        }

        CrossNFC.Current.OnTagDiscovered += TagHandler;

        if (!_startedPublishing)
        {
            CrossNFC.Current.StartPublishing();
            _startedPublishing = true;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            tcs.TrySetCanceled();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            Cleanup();
        }
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
