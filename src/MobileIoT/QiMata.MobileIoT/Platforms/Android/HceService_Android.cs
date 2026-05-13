#if ANDROID && TEST_HARNESS
using Android.App;
using Android.Nfc;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.Platforms.Android;

public class HceService_Android : IHceService
{
    public bool IsAvailable => NfcAdapter.GetDefaultAdapter(global::Android.App.Application.Context) is not null;

    public byte[]? LastSelectedAid =>
        HceApduService.LastSelectedAid.Length == 0 ? null : HceApduService.LastSelectedAid;

    public bool WasSelected => HceApduService.LastSelectedAid.Length > 0;

    public event EventHandler<byte[]>? PayloadServed
    {
        add { if (value is not null) HceApduService.PayloadServedInternalAdd(value); }
        remove { if (value is not null) HceApduService.PayloadServedInternalRemove(value); }
    }

    public Task StartAsync(byte[] aid, byte[] payload, CancellationToken cancellationToken)
    {
        HceApduService.LastSelectedAid = Array.Empty<byte>();
        HceApduService.PendingPayload = payload;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        HceApduService.PendingPayload = null;
        return Task.CompletedTask;
    }
}
#endif
