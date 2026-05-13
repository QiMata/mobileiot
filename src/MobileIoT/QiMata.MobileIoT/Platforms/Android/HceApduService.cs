#if ANDROID && TEST_HARNESS
using Android.App;
using Android.Content;
using Android.Nfc.CardEmulators;
using Android.OS;

namespace QiMata.MobileIoT.Platforms.Android;

[Service(Exported = true, Permission = "android.permission.BIND_NFC_SERVICE")]
[IntentFilter(new[] { "android.nfc.cardemulation.action.HOST_APDU_SERVICE" })]
[MetaData("android.nfc.cardemulation.host_apdu_service", Resource = "@xml/apduservice")]
public class HceApduService : HostApduService
{
    internal static volatile byte[]? PendingPayload;
    internal static volatile byte[] LastSelectedAid = Array.Empty<byte>();
    static EventHandler<byte[]>? _payloadServed;
    static readonly object _eventLock = new();

    internal static void PayloadServedInternalAdd(EventHandler<byte[]> handler)
    {
        lock (_eventLock) _payloadServed += handler;
    }

    internal static void PayloadServedInternalRemove(EventHandler<byte[]> handler)
    {
        lock (_eventLock) _payloadServed -= handler;
    }

    static readonly byte[] StatusOk = { 0x90, 0x00 };
    static readonly byte[] StatusUnknown = { 0x6D, 0x00 };
    static readonly byte[] StatusFileNotFound = { 0x6A, 0x82 };

    public override byte[] ProcessCommandApdu(byte[]? commandApdu, Bundle? extras)
    {
        if (commandApdu is null || commandApdu.Length < 4)
            return StatusUnknown;

        if (!IsSelectAid(commandApdu))
            return StatusUnknown;

        if (commandApdu.Length < 6)
            return StatusUnknown;

        int aidLen = commandApdu[4];
        if (aidLen < 5 || commandApdu.Length < 5 + aidLen)
            return StatusUnknown;

        var aid = new byte[aidLen];
        Buffer.BlockCopy(commandApdu, 5, aid, 0, aidLen);
        LastSelectedAid = aid;

        var payload = PendingPayload;
        if (payload is null)
            return StatusFileNotFound;

        var response = new byte[payload.Length + 2];
        Buffer.BlockCopy(payload, 0, response, 0, payload.Length);
        response[^2] = 0x90;
        response[^1] = 0x00;
        EventHandler<byte[]>? handler;
        lock (_eventLock) handler = _payloadServed;
        handler?.Invoke(null, payload);
        return response;
    }

    public override void OnDeactivated(DeactivationReason reason) { }

    static bool IsSelectAid(byte[] apdu) =>
        apdu[0] == 0x00 && apdu[1] == 0xA4 && apdu[2] == 0x04 && apdu[3] == 0x00;
}
#endif
