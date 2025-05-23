using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.Platforms.iOS;

public class NfcP2PService_iOS : INfcP2PService
{
    public void StartP2P() =>
        throw new NotSupportedException("NFC peer-to-peer is not available on iOS.");

    public void StopP2P() { }
}
