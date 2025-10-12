using System;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services.Interfaces;

public interface IAudioDecoder
{
    ValueTask<string?> TryDecodeAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}
