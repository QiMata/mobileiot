using System.Diagnostics;
using QiMata.MobileIoT.ThreadDemoCore.Models;

namespace QiMata.MobileIoT.ThreadDemoCore.Services;

public sealed class ThreadDemoService : IThreadDemoService
{
    private readonly IThreadBridgeClient _bridgeClient;

    public ThreadDemoService(IThreadBridgeClient bridgeClient)
    {
        _bridgeClient = bridgeClient;
    }

    public async Task<ThreadStatusSnapshot> GetStatusAsync(ThreadConnectionSettings settings, CancellationToken ct = default)
    {
        if (!settings.UseLiveBridge)
            return GetMockStatus();

        try
        {
            return await _bridgeClient.GetStatusAsync(settings.BridgeBaseUrl, settings.TimeoutMs, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Bridge unreachable: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Bridge error: {ex.Message}", ex);
        }
    }

    public async Task<ThreadPingResult> SendPingAsync(ThreadConnectionSettings settings, string payload, CancellationToken ct = default)
    {
        if (!settings.UseLiveBridge)
            return GetMockPing(payload);

        try
        {
            return await _bridgeClient.SendPingAsync(
                settings.BridgeBaseUrl,
                settings.TargetNode,
                payload,
                settings.TimeoutMs,
                ct);
        }
        catch (OperationCanceledException)
        {
            return new ThreadPingResult
            {
                Success = false,
                RequestPayload = payload,
                Error = "Request timed out"
            };
        }
        catch (HttpRequestException ex)
        {
            return new ThreadPingResult
            {
                Success = false,
                RequestPayload = payload,
                Error = $"Bridge unreachable: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ThreadPingResult
            {
                Success = false,
                RequestPayload = payload,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    private static ThreadStatusSnapshot GetMockStatus() => new()
    {
        Role = "leader",
        DatasetHex = "0e080000000000010000000300001235060004001fffe00208dead00beef00cafe0708fddead00beef0000051000112233445566778899aabbccddeeff030f4f70656e5468726561642d633162370102c1b70410445f2b5ca6f2a93a5535013e26a118430c0402a0f7f8",
        MeshLocalAddresses = ["fddead:00be:ef00:0:0:ff:fe00:fc00", "fddead:00be:ef00:0:a8cd:baff:dead:beef"],
        Rloc16 = "0x0400",
        LastUpdatedUtc = DateTime.UtcNow,
        SourceMode = "mock"
    };

    private static ThreadPingResult GetMockPing(string payload)
    {
        var sw = Stopwatch.StartNew();
        // Simulate a small delay
        Thread.Sleep(Random.Shared.Next(5, 25));
        sw.Stop();

        return new ThreadPingResult
        {
            Success = true,
            RequestPayload = payload,
            ResponsePayload = $"ECHO: {payload}",
            RoundTripMs = sw.Elapsed.TotalMilliseconds
        };
    }
}
