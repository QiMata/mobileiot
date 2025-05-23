namespace QiMata.MobileIoT.Services;

public interface INfcP2PService
{
    void StartP2P();   // advertise an NDEF message
    void StopP2P();    // optional: unregister callbacks
}
