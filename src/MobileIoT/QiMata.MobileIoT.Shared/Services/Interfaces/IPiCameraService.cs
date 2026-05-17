namespace QiMata.MobileIoT.Shared.Services.Interfaces;

public interface IPiCameraService
{
    Task<Stream?> CaptureFrameAsync(string piAddress, CancellationToken ct = default);
    Task<(string label, double confidence)?> ClassifyRemoteAsync(string piAddress, CancellationToken ct = default);
    Task<bool> CheckHealthAsync(string piAddress, CancellationToken ct = default);
}
