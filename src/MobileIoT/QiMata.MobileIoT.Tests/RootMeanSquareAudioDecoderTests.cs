using QiMata.MobileIoT.Services;
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
}
