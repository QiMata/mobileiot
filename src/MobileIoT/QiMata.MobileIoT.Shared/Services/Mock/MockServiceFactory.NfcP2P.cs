using Moq;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System.Collections.Generic;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
        public static INfcP2PService CreateNfcP2PService()
        {
            // public (or InternalsVisibleTo‐exposed) extra interface
            var mock = new Mock<INfcP2PService>();

            var sent = new List<string>();

            mock.Setup(m => m.StartP2P(It.IsAny<string>()))
                .Callback<string>(msg => sent.Add(msg));

            mock.Setup(m => m.StopP2P());

            // add test-only helpers
            mock.As<INfcP2PTestHarness>()
                .SetupGet(h => h.SentMessages)
                .Returns(sent);

            mock.As<INfcP2PTestHarness>()
                .Setup(h => h.SimulateIncoming(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                .Callback<string, string, byte[]>((mime, text, raw) =>
                {
                    raw ??= System.Text.Encoding.UTF8.GetBytes(text);
                    mock.Raise(s => s.MessageReceived += null,
                        new NfcMessageEventArgs(mime, text, raw));
                });

            return mock.Object;
        }
    }
}
