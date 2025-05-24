namespace QiMata.MobileIoT.Services.I;

public interface INfcP2PService
{
    /// <summary>
    /// Begin advertising the specified text as an NDEF MIME record.
    /// </summary>
    /// <param name="textToSend">Text payload to advertise.</param>
    void StartP2P(string textToSend);

    /// <summary>Stop advertising / unregister callbacks.</summary>
    void StopP2P();

    /// <summary>
    /// Raised when an NDEF message arrives from a peer.
    /// </summary>
    event EventHandler<NfcMessageEventArgs> MessageReceived;
}

/// <summary>Details extracted from an incoming NDEF record.</summary>
public sealed class NfcMessageEventArgs : EventArgs
{
    public string MimeType { get; }
    public string Text { get; }
    public byte[] RawPayload { get; }

    public NfcMessageEventArgs(string mimeType, string text, byte[] rawPayload)
    {
        MimeType = mimeType;
        Text = text;
        RawPayload = rawPayload;
    }
}
