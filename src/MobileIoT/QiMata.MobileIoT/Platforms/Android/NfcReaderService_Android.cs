#if ANDROID && TEST_HARNESS
using Android.Nfc;
using Android.Nfc.Tech;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.Platforms.Android;

public class NfcReaderService_Android : Java.Lang.Object, INfcReaderService, NfcAdapter.IReaderCallback
{
    TaskCompletionSource<byte[]>? _tcs;
    byte[]? _aid;

    public bool IsAvailable
    {
        get
        {
            var activity = MainActivity.Current;
            if (activity is null) return false;
            var adapter = NfcAdapter.GetDefaultAdapter(activity);
            return adapter is not null && adapter.IsEnabled;
        }
    }

    public async Task<byte[]?> ReadOnceAsync(byte[] aid, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var activity = MainActivity.Current
            ?? throw new InvalidOperationException("MainActivity is not foreground");
        var adapter = NfcAdapter.GetDefaultAdapter(activity)
            ?? throw new InvalidOperationException("No NFC adapter on this device");

        _aid = aid;
        _tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var flags = NfcReaderFlags.NfcA | NfcReaderFlags.SkipNdefCheck;

        activity.RunOnUiThread(() => adapter.EnableReaderMode(activity, this, flags, null));
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return await _tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            activity.RunOnUiThread(() => adapter.DisableReaderMode(activity));
            _tcs = null;
            _aid = null;
        }
    }

    public void OnTagDiscovered(Tag? tag)
    {
        var tcs = _tcs;
        var aid = _aid;
        if (tcs is null || aid is null || tag is null) return;

        try
        {
            using var iso = IsoDep.Get(tag);
            if (iso is null)
            {
                tcs.TrySetResult(Array.Empty<byte>());
                return;
            }
            iso.Connect();
            try
            {
                var apdu = BuildSelectAidApdu(aid);
                var response = iso.Transceive(apdu);
                tcs.TrySetResult(response ?? Array.Empty<byte>());
            }
            finally
            {
                iso.Close();
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    static byte[] BuildSelectAidApdu(byte[] aid)
    {
        var apdu = new byte[5 + aid.Length];
        apdu[0] = 0x00;
        apdu[1] = 0xA4;
        apdu[2] = 0x04;
        apdu[3] = 0x00;
        apdu[4] = (byte)aid.Length;
        Buffer.BlockCopy(aid, 0, apdu, 5, aid.Length);
        return apdu;
    }
}
#endif
