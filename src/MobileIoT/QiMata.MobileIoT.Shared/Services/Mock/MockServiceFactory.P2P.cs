using Moq;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Mock
{
    public static partial class MockServiceFactory
    {
        public static IP2PService CreateP2PService(Action<Mock<IP2PService>>? configure = null)
        {
            var mock = new Mock<IP2PService>(MockBehavior.Strict);

            var connectedPeers = new HashSet<string>();
            var channel = Channel.CreateUnbounded<(string PeerId, ReadOnlyMemory<byte> Data)>();

            mock.Setup(s => s.StartDiscoveryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            mock.Setup(s => s.ConnectToPeerAsync(It.IsAny<string>(),
                                                 It.IsAny<CancellationToken>()))
                .Returns((string peerId, CancellationToken _) =>
                {
                    connectedPeers.Add(peerId);
                    return Task.FromResult(true);
                });

            mock.Setup(s => s.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(),
                                        It.IsAny<string?>(),
                                        It.IsAny<CancellationToken>()))
                .Returns((ReadOnlyMemory<byte> buffer, string? peerId, CancellationToken _) =>
                {
                    var target = peerId ?? "broadcast";
                    channel.Writer.TryWrite((target, buffer));
                    return Task.FromResult(true);
                });

            mock.Setup(s => s.ReceiveAsync(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken ct) => ReadMessagesAsync(ct));

            mock.Setup(s => s.StopAsync())
                .Returns(() =>
                {
                    channel.Writer.TryComplete();
                    return Task.CompletedTask;
                });

            configure?.Invoke(mock);
            return mock.Object;

            async IAsyncEnumerable<(string PeerId, ReadOnlyMemory<byte> Data)> ReadMessagesAsync(
                [EnumeratorCancellation] CancellationToken ct)
            {
                while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var message))
                    {
                        yield return message;
                    }
                }
            }
        }
    }
}
