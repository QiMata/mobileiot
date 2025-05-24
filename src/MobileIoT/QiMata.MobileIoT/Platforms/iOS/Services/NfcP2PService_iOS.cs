using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Platforms.iOS;

public class NfcP2PService_iOS : INfcP2PService
{
    public event EventHandler<NfcMessageEventArgs>? MessageReceived;

    public void StartP2P(string _) =>
        throw new NotSupportedException("NFC peer-to-peer is not supported on iOS.");

    public void StopP2P() { }
}
