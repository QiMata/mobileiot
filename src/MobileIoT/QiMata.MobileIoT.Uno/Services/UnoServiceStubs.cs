using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Uno.Services;

public sealed class UnoQrScanningService : IQrScanningService
{
    public Task<string?> ScanAsync() => Task.FromResult<string?>("UNO-QR-STUB");
}

public sealed class UnoImageClassificationService : IImageClassificationService
{
    public string ClassifyImage(Stream imageStream) => "Uno local classifier stub";
}

public sealed class UnoPiCameraService(HttpClient http) : IPiCameraService
{
    public async Task<Stream?> CaptureFrameAsync(string piAddress, CancellationToken ct = default)
    {
        try
        {
            return await http.GetStreamAsync($"http://{piAddress.TrimEnd('/')}/camera/frame", ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(string label, double confidence)?> ClassifyRemoteAsync(string piAddress, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync($"http://{piAddress.TrimEnd('/')}/camera/classify", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;
            return ("remote-classification", 0);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CheckHealthAsync(string piAddress, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync($"http://{piAddress.TrimEnd('/')}/health", ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UnoAudioModemService : IAudioModemService
{
    public event EventHandler<string>? DataReceived;

    public Task StartAsync(CancellationToken ct = default)
    {
        DataReceived?.Invoke(this, "Uno audio modem stub");
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;
}
