using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.ThreadDemoCore.Models;
using QiMata.MobileIoT.ThreadDemoCore.Services;

namespace QiMata.MobileIoT.ViewModels;

public partial class ThreadViewModel : ObservableObject
{
    private readonly IThreadDemoService _threadService;
    private readonly INavigationService _navigation;

    public const int MaxLogEntries = 200;

    public ThreadViewModel(IThreadDemoService threadService, INavigationService navigation)
    {
        _threadService = threadService;
        _navigation = navigation;
    }

    // --- Connection settings ---
    [ObservableProperty] private bool _useLiveBridge;
    [ObservableProperty] private string _bridgeUrl = "http://raspberrypi.local:8080";
    [ObservableProperty] private string _targetNode = string.Empty;
    [ObservableProperty] private string _payload = "ping";

    // --- Status display ---
    [ObservableProperty] private string _role = "--";
    [ObservableProperty] private string _datasetHex = "--";
    [ObservableProperty] private string _meshAddresses = "--";
    [ObservableProperty] private string _rloc16 = "--";
    [ObservableProperty] private string _sourceMode = "--";

    // --- Busy state ---
    [ObservableProperty] private bool _isBusy;

    // --- Log ---
    public ObservableCollection<ThreadLogEntry> LogEntries { get; } = [];

    [ObservableProperty] private string _logText = string.Empty;

    private ThreadConnectionSettings BuildSettings() => new()
    {
        UseLiveBridge = UseLiveBridge,
        BridgeBaseUrl = BridgeUrl,
        TargetNode = TargetNode,
        TimeoutMs = 3000
    };

    private void AppendLog(string level, string message)
    {
        var entry = new ThreadLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            Message = message
        };

        LogEntries.Add(entry);

        while (LogEntries.Count > MaxLogEntries)
            LogEntries.RemoveAt(0);

        LogText = string.Join('\n', LogEntries.Select(e => e.ToString()));
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task RefreshStatus()
    {
        IsBusy = true;
        RefreshStatusCommand.NotifyCanExecuteChanged();
        SendPingCommand.NotifyCanExecuteChanged();

        try
        {
            AppendLog("INFO", "Fetching Thread status...");
            var status = await _threadService.GetStatusAsync(BuildSettings());

            Role = status.Role;
            DatasetHex = status.DatasetHex;
            MeshAddresses = string.Join(", ", status.MeshLocalAddresses);
            Rloc16 = status.Rloc16;
            SourceMode = status.SourceMode;

            AppendLog("INFO", $"Status refreshed ({status.SourceMode} mode): role={status.Role}");
        }
        catch (OperationCanceledException)
        {
            AppendLog("WARN", "Status request was cancelled");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR", ex.Message);
        }
        finally
        {
            IsBusy = false;
            RefreshStatusCommand.NotifyCanExecuteChanged();
            SendPingCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task SendPing()
    {
        IsBusy = true;
        RefreshStatusCommand.NotifyCanExecuteChanged();
        SendPingCommand.NotifyCanExecuteChanged();

        try
        {
            var pingPayload = string.IsNullOrWhiteSpace(Payload) ? "ping" : Payload;
            AppendLog("INFO", $"Sending CoAP ping: \"{pingPayload}\"...");

            var result = await _threadService.SendPingAsync(BuildSettings(), pingPayload);

            if (result.Success)
            {
                AppendLog("INFO", $"Ping OK: \"{result.ResponsePayload}\" RTT={result.RoundTripMs:F1}ms");
            }
            else
            {
                AppendLog("ERROR", $"Ping failed: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            AppendLog("ERROR", ex.Message);
        }
        finally
        {
            IsBusy = false;
            RefreshStatusCommand.NotifyCanExecuteChanged();
            SendPingCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        LogText = string.Empty;
    }

    [RelayCommand]
    private Task NavigateBack() => _navigation.GoBackAsync();

    private bool CanExecute() => !IsBusy;
}
