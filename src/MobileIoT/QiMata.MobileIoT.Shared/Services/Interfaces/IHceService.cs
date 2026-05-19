namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Controls the Host Card Emulation (HCE) service that makes the device respond to NFC readers as a contactless card.</summary>
public interface IHceService
{
    /// <summary>Indicates whether HCE is supported on this device.</summary>
    bool IsAvailable { get; }

    /// <summary>Returns the last AID that was selected by an NFC reader, or null if none has been selected.</summary>
    byte[]? LastSelectedAid { get; }

    /// <summary>Indicates whether the HCE service has been selected by an NFC reader during the current session.</summary>
    bool WasSelected { get; }

    /// <summary>Registers the given AID and payload and starts the HCE service so readers can retrieve the payload.</summary>
    Task StartAsync(byte[] aid, byte[] payload, CancellationToken cancellationToken);

    /// <summary>Stops the HCE service and unregisters the AID.</summary>
    Task StopAsync();

    /// <summary>Raised each time an NFC reader successfully reads the emulated card payload.</summary>
    event EventHandler<byte[]>? PayloadServed;
}
