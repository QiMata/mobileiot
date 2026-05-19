using System.Net.Http.Json;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Services;

/// <summary>HTTP client that communicates with the Pi Camera HTTP server to capture frames, run remote classification, and check service health.</summary>
public class PiCameraService : IPiCameraService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Downloads a single JPEG frame from the Pi Camera server at the given address.</summary>
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

    /// <summary>Requests image classification from the Pi Camera server and returns the top label and confidence score.</summary>
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

    /// <summary>Pings the Pi Camera server health endpoint and returns true if it responds with a success status.</summary>
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
