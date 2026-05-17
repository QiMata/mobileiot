namespace QiMata.MobileIoT.Shared.Services.Interfaces;

public interface INfcReaderService
{
    bool IsAvailable { get; }
    Task<byte[]?> ReadOnceAsync(byte[] aid, TimeSpan timeout, CancellationToken cancellationToken);
}
