using System.Net.Http.Json;
using System.Text.Json;
using QiMata.MobileIoT.ThreadDemoCore.Models;

namespace QiMata.MobileIoT.ThreadDemoCore.Services;

public sealed class ThreadBridgeClient : IThreadBridgeClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ThreadBridgeClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ThreadStatusSnapshot> GetStatusAsync(string baseUrl, int timeoutMs, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var url = $"{baseUrl.TrimEnd('/')}/thread/status";
        var response = await _http.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<StatusResponse>(JsonOptions, cts.Token)
                   ?? throw new InvalidOperationException("Empty response from bridge");

        if (!json.Ok)
            throw new InvalidOperationException($"Bridge returned error for status");

        return new ThreadStatusSnapshot
        {
            Role = json.Role ?? string.Empty,
            DatasetHex = json.DatasetHex ?? string.Empty,
            MeshLocalAddresses = json.MeshLocalAddresses ?? [],
            Rloc16 = json.Rloc16 ?? string.Empty,
            LastUpdatedUtc = DateTime.UtcNow,
            SourceMode = "live"
        };
    }

    public async Task<ThreadPingResult> SendPingAsync(string baseUrl, string target, string payload, int timeoutMs, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var url = $"{baseUrl.TrimEnd('/')}/thread/ping";
        var body = new { target, payload, timeoutMs };
        var response = await _http.PostAsJsonAsync(url, body, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<PingResponse>(JsonOptions, cts.Token)
                   ?? throw new InvalidOperationException("Empty response from bridge");

        return new ThreadPingResult
        {
            Success = json.Ok,
            RequestPayload = payload,
            ResponsePayload = json.Response ?? string.Empty,
            RoundTripMs = json.RttMs,
            Error = json.Error
        };
    }

    private sealed class StatusResponse
    {
        public bool Ok { get; set; }
        public string? Role { get; set; }
        public string? DatasetHex { get; set; }
        public List<string>? MeshLocalAddresses { get; set; }
        public string? Rloc16 { get; set; }
        public string? TimestampUtc { get; set; }
    }

    private sealed class PingResponse
    {
        public bool Ok { get; set; }
        public string? Payload { get; set; }
        public string? Response { get; set; }
        public double RttMs { get; set; }
        public string? Error { get; set; }
    }
}
