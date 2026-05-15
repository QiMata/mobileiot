using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Models;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class WifiDirectViewModel : BaseViewModel
{
    private readonly IP2PService _p2p;
    private CancellationTokenSource? _receiveCts;
    private readonly List<byte> _rxBuffer = new();

    [ObservableProperty] string _connectionStatus = "Disconnected";
    [ObservableProperty] string _connectButtonText = "Discover";
    [ObservableProperty] double _temperature;
    [ObservableProperty] double _humidity;
    [ObservableProperty] double _cpuTemp;
    [ObservableProperty] string _uptime = "--";
    [ObservableProperty] bool _isStreaming;
    [ObservableProperty] string _streamButtonText = "Start Stream";
    [ObservableProperty] string _log = string.Empty;

    public ObservableCollection<RemoteFileInfo> Files { get; } = new();

    private bool _isConnected;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WifiDirectViewModel(IP2PService p2p, IAppLogger logger) : base(logger)
    {
        _p2p = p2p;
    }

    private void AppendLine(string message)
    {
        Log += message + "\n";
        Logger.Info(message);
    }

    private async Task SendJsonAsync(object msg, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(msg, JsonOpts);
        var payload = Encoding.UTF8.GetBytes(json);
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame, 4);
        await _p2p.SendAsync(frame, ct: ct);
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        if (_isConnected)
        {
            _receiveCts?.Cancel();
            await _p2p.StopAsync();
            _isConnected = false;
            IsStreaming = false;
            StreamButtonText = "Start Stream";
            ConnectionStatus = "Disconnected";
            ConnectButtonText = "Discover";
            AppendLine("Disconnected");
            return;
        }

        ConnectionStatus = "Discovering...";
        AppendLine("Discovering peers...");
        bool ok = await _p2p.StartDiscoveryAsync();
        if (!ok)
        {
            ConnectionStatus = "Discovery failed";
            AppendLine("Discovery failed");
            return;
        }

        ConnectionStatus = "Connected";
        ConnectButtonText = "Disconnect";
        _isConnected = true;
        AppendLine("Connected");

        _receiveCts = new CancellationTokenSource();
        FireAndForget(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var (_, data) in _p2p.ReceiveAsync(ct))
            {
                _rxBuffer.AddRange(data.ToArray());
                ProcessFrames();
            }
        }
        catch (OperationCanceledException) { }
    }

    private void ProcessFrames()
    {
        while (_rxBuffer.Count >= 4)
        {
            var lenBytes = _rxBuffer.GetRange(0, 4).ToArray();
            uint len = BinaryPrimitives.ReadUInt32BigEndian(lenBytes);
            if (len > 10 * 1024 * 1024)
            {
                _rxBuffer.Clear();
                return;
            }
            if (_rxBuffer.Count < 4 + (int)len)
                return;

            var payload = _rxBuffer.GetRange(4, (int)len).ToArray();
            _rxBuffer.RemoveRange(0, 4 + (int)len);

            var json = Encoding.UTF8.GetString(payload);
            DispatchMessage(json);
        }
    }

    private void DispatchMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();

            OnMain(() =>
            {
                switch (type)
                {
                    case "telemetry":
                        if (doc.RootElement.TryGetProperty("temp", out var t))
                            Temperature = t.GetDouble();
                        if (doc.RootElement.TryGetProperty("hum", out var h))
                            Humidity = h.GetDouble();
                        if (doc.RootElement.TryGetProperty("cpu_temp", out var c))
                            CpuTemp = c.GetDouble();
                        if (doc.RootElement.TryGetProperty("uptime", out var u))
                        {
                            var secs = (int)u.GetDouble();
                            Uptime = $"{secs / 3600}h {secs % 3600 / 60}m {secs % 60}s";
                        }
                        break;

                    case "file_list_response":
                        Files.Clear();
                        if (doc.RootElement.TryGetProperty("files", out var files))
                        {
                            foreach (var f in files.EnumerateArray())
                            {
                                var name = f.GetProperty("name").GetString() ?? "";
                                var size = f.GetProperty("size").GetInt64();
                                Files.Add(new RemoteFileInfo(name, size));
                            }
                        }
                        AppendLine($"Received file list: {Files.Count} files");
                        break;

                    case "file_data":
                        var fileName = doc.RootElement.GetProperty("name").GetString() ?? "download";
                        var b64 = doc.RootElement.GetProperty("data").GetString() ?? "";
                        if (!string.IsNullOrEmpty(b64))
                        {
                            var bytes = Convert.FromBase64String(b64);
                            var path = Path.Combine(FileSystem.AppDataDirectory, fileName);
                            File.WriteAllBytes(path, bytes);
                            AppendLine($"Downloaded: {fileName} ({bytes.Length} bytes) -> {path}");
                        }
                        else
                        {
                            AppendLine($"Download failed: {fileName}");
                        }
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            OnMain(() => AppendLine($"Parse error: {ex.Message}"));
            Logger.Warn("DispatchMessage parse error", ex);
        }
    }

    [RelayCommand]
    private async Task ToggleStreamAsync()
    {
        if (!_isConnected) return;

        if (!IsStreaming)
        {
            await SendJsonAsync(new { type = "start_telemetry" });
            IsStreaming = true;
            StreamButtonText = "Stop Stream";
            AppendLine("Telemetry streaming started");
        }
        else
        {
            await SendJsonAsync(new { type = "stop_telemetry" });
            IsStreaming = false;
            StreamButtonText = "Start Stream";
            AppendLine("Telemetry streaming stopped");
        }
    }

    [RelayCommand]
    private async Task RequestFileListAsync()
    {
        if (!_isConnected) return;
        await SendJsonAsync(new { type = "file_list", path = AppConstants.Hosts.PiDataPath });
        AppendLine("Requested file list");
    }

    [RelayCommand]
    private async Task DownloadFileAsync(RemoteFileInfo? file)
    {
        if (!_isConnected || file is null) return;
        await SendJsonAsync(new { type = "file_download", path = file.Name });
        AppendLine($"Requested download: {file.Name}");
    }

    [RelayCommand]
    private Task NavigateBack() => Shell.Current.GoToAsync("..");
}
