using System.Net.Http.Json;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Services;

public class PiCameraService : IPiCameraService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<Stream?> CaptureFrameAsync(string piAddress, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"http://{piAddress}:5000/frame", ct);
            if (!response.IsSuccessStatusCode) return null;
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return new MemoryStream(bytes);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(string label, double confidence)?> ClassifyRemoteAsync(string piAddress, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"http://{piAddress}:5000/classify", ct);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<ClassifyResult>(ct);
            return result is null ? null : (result.Label, result.Confidence);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CheckHealthAsync(string piAddress, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"http://{piAddress}:5000/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private record ClassifyResult(string Label, double Confidence);
}
