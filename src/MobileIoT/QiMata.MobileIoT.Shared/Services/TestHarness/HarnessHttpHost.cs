#if TEST_HARNESS
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QiMata.MobileIoT.Services.TestHarness.Scenarios;

namespace QiMata.MobileIoT.Services.TestHarness;

/// <summary>
/// Localhost-only HTTP hook exposed when the app is compiled with the TEST_HARNESS flag AND the
/// MIOT_TEST_MODE env var / intent extra is set. Bound to 127.0.0.1:47821; the test harness on the
/// dev box reaches it through `adb forward` (Android) or usbmux forward (iOS).
///
/// DO NOT remove the TEST_HARNESS guard. Release builds must not ship this code.
/// </summary>
public sealed class HarnessHttpHost : IDisposable
{
    public const int Port = 47821;

    private readonly ScenarioRegistry _scenarios;
    private readonly ILogger<HarnessHttpHost> _logger;
    private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
    private readonly int _logBufferMax = 2000;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private IScenario? _active;
    private long _logSeq;

    public HarnessHttpHost(ScenarioRegistry scenarios, ILogger<HarnessHttpHost> logger)
    {
        _scenarios = scenarios;
        _logger = logger;
    }

    public void Start()
    {
        if (_listener is not null) return;
        var prefix = $"http://127.0.0.1:{Port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _logger.LogInformation("HarnessHttpHost listening on {Prefix}", prefix);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener!.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(ctx, ct));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (path == "/health")
                await WriteJson(ctx, 200, new { ok = true, scenarios = _scenarios.Names });
            else if (path.StartsWith("/scenario/") && ctx.Request.HttpMethod == "POST")
                await HandleStartScenarioAsync(ctx, path.Substring("/scenario/".Length), ct);
            else if (path == "/state")
                await HandleStateAsync(ctx, ct);
            else if (path == "/inject" && ctx.Request.HttpMethod == "POST")
                await HandleInjectAsync(ctx, ct);
            else if (path == "/logs")
                await HandleLogsAsync(ctx);
            else
                await WriteJson(ctx, 404, new { ok = false, error = "not_found", path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "harness handler failed");
            try { await WriteJson(ctx, 500, new { ok = false, error = ex.Message }); } catch { }
        }
    }

    private async Task HandleStartScenarioAsync(HttpListenerContext ctx, string name, CancellationToken ct)
    {
        if (!_scenarios.TryGet(name, out var scenario))
        {
            await WriteJson(ctx, 404, new { ok = false, error = "unknown_scenario", name });
            return;
        }
        var args = await ReadJsonDictAsync(ctx.Request);
        if (_active is not null)
            await _active.StopAsync(ct);
        _active = scenario;
        var result = await scenario.StartAsync(args, ct);
        await WriteJson(ctx, 200, new { ok = true, name = scenario.Name, result });
    }

    private async Task HandleStateAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        if (_active is null)
        {
            await WriteJson(ctx, 200, new { ok = true, active = (string?)null });
            return;
        }
        var state = await _active.GetStateAsync(ct);
        await WriteJson(ctx, 200, new { ok = true, active = _active.Name, state });
    }

    private async Task HandleInjectAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        // Placeholder wired-up contract. Per-integration plans add concrete injectors that
        // feed MockServiceFactory channels (BLE notification, NFC payload, etc.).
        var payload = await ReadJsonDictAsync(ctx.Request);
        await WriteJson(ctx, 200, new { ok = true, accepted = payload });
    }

    private Task HandleLogsAsync(HttpListenerContext ctx)
    {
        var sinceParam = ctx.Request.QueryString["since"];
        long since = 0;
        long.TryParse(sinceParam, out since);
        var entries = new List<object>();
        foreach (var e in _logBuffer)
            if (e.Seq > since)
                entries.Add(new { seq = e.Seq, level = e.Level, message = e.Message });
        return WriteJson(ctx, 200, new { ok = true, entries });
    }

    public void Log(string level, string message)
    {
        var entry = new LogEntry(Interlocked.Increment(ref _logSeq), level, message);
        _logBuffer.Enqueue(entry);
        while (_logBuffer.Count > _logBufferMax && _logBuffer.TryDequeue(out _)) { }
    }

    private static async Task<Dictionary<string, object?>> ReadJsonDictAsync(HttpListenerRequest req)
    {
        if (req.ContentLength64 <= 0) return new Dictionary<string, object?>();
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body)) return new Dictionary<string, object?>();
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(body) ?? new Dictionary<string, object?>();
    }

    private static async Task WriteJson(HttpListenerContext ctx, int status, object payload)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
        _listener = null;
    }

    private readonly record struct LogEntry(long Seq, string Level, string Message);
}
#endif
