namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>Communicates with the Pi Camera HTTP server to retrieve frames, run remote ML classification, and verify server health.</summary>
public interface IPiCameraService
{
    /// <summary>Downloads a single JPEG frame from the Pi Camera server at the given address.</summary>
    Task<Stream?> CaptureFrameAsync(string piAddress, CancellationToken ct = default);

    /// <summary>Requests image classification from the Pi Camera server and returns the top label and confidence score.</summary>
    Task<(string label, double confidence)?> ClassifyRemoteAsync(string piAddress, CancellationToken ct = default);

    /// <summary>Checks whether the Pi Camera server is reachable and returning a healthy status.</summary>
    Task<bool> CheckHealthAsync(string piAddress, CancellationToken ct = default);
}
