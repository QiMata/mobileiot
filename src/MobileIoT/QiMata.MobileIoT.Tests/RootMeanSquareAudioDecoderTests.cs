using QiMata.MobileIoT.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QiMata.MobileIoT.Tests;

public class RootMeanSquareAudioDecoderTests
{
    [Fact]
    public async Task TryDecodeAsync_ReturnsNullForSilence()
    {
        var decoder = new RootMeanSquareAudioDecoder();
        var buffer = new byte[64];

        var result = await decoder.TryDecodeAsync(buffer, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDecodeAsync_ReturnsMessageForSignal()
    {
        var decoder = new RootMeanSquareAudioDecoder();
        var buffer = new byte[64];
        for (int i = 0; i < buffer.Length; i += 2)
        {
            buffer[i] = 0xFF;
            buffer[i + 1] = 0x7F;
        }

        var result = await decoder.TryDecodeAsync(buffer, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("RMS amplitude", result);
    }

    [Fact]
    public async Task TryDecodeAsync_ReturnsNullForLowAmplitude()
    {
        var decoder = new RootMeanSquareAudioDecoder();
        var buffer = new byte[16];
        for (int i = 0; i < buffer.Length; i += 2)
        {
            buffer[i] = 0x10;
            buffer[i + 1] = 0x00;
        }

        var result = await decoder.TryDecodeAsync(buffer, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDecodeAsync_HandlesOddLengthBuffers()
    {
        var decoder = new RootMeanSquareAudioDecoder();
        var buffer = new byte[] { 0x00, 0x80, 0x00, 0x80, 0xFF };

        var result = await decoder.TryDecodeAsync(buffer, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("2 samples", result);
    }

    [Fact]
    public async Task TryDecodeAsync_RespectsCancellation()
    {
        var decoder = new RootMeanSquareAudioDecoder();
        var buffer = new byte[64];
        for (int i = 0; i < buffer.Length; i += 2)
        {
            buffer[i] = 0xFF;
            buffer[i + 1] = 0x7F;
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => decoder.TryDecodeAsync(buffer, cts.Token).AsTask());
    }
}
