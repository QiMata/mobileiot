using System.Collections.Generic;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public interface INfcP2PTestHarness
    {
        IReadOnlyList<string> SentMessages { get; }
        void SimulateIncoming(string mimeType, string text, byte[]? rawPayload = null);
    }
}
