using System;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Attempts to decode a raw audio byte buffer into a text string.</summary>
public interface IAudioDecoder
{
    /// <summary>Tries to decode the supplied audio buffer and returns the decoded text, or null if no message was recognized.</summary>
    ValueTask<string?> TryDecodeAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}
