namespace QiMata.MobileIoT.Shared.Services;

/// <summary>Opens the device camera to scan a QR code and returns the decoded text payload.</summary>
public interface IQrScanningService
{
    /// <summary>Activates the QR scanner and returns the decoded string when a code is recognized, or null if the user cancels.</summary>
    Task<string?> ScanAsync();
}
