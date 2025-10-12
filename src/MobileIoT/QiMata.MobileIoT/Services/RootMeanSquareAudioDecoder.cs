using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.Services;

public sealed class RootMeanSquareAudioDecoder : IAudioDecoder
{
    public ValueTask<string?> TryDecodeAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length < 4)
        {
            return ValueTask.FromResult<string?>(null);
        }

        ReadOnlySpan<byte> span = buffer.Span;
        double sumSquares = 0;
        int samples = 0;

        for (int i = 0; i + 1 < span.Length; i += 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            short sample = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(i, 2));
            sumSquares += sample * (double)sample;
            samples++;
        }

        if (samples == 0)
        {
            return ValueTask.FromResult<string?>(null);
        }

        double rms = Math.Sqrt(sumSquares / samples) / short.MaxValue;
        if (rms < 0.01)
        {
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult<string?>($"RMS amplitude {(rms * 100):F1}% across {samples} samples");
    }
}
