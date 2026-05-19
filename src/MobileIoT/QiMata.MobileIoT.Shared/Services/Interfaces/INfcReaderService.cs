namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Reads a single APDU response from an NFC tag or emulated card matching the given AID.</summary>
public interface INfcReaderService
{
    /// <summary>Indicates whether NFC reading is supported on this device.</summary>
    bool IsAvailable { get; }

    /// <summary>Waits for an NFC tag presenting the specified AID and returns its payload, or null if the timeout elapses.</summary>
    Task<byte[]?> ReadOnceAsync(byte[] aid, TimeSpan timeout, CancellationToken cancellationToken);
}
