using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System;
using System.Threading;

namespace QiMata.MobileIoT.ViewModels;

public partial class BleViewModel : BaseViewModel
{
    private readonly IBleDemoService _ble;
    private bool _isConnected;

    public BleViewModel(IBleDemoService ble, IAppLogger logger) : base(logger)
    {
        _ble = ble;
        Subscribe<(double temp, double humidity)>(
            h => _ble.SensorDataReceived += h,
            h => _ble.SensorDataReceived -= h,
            OnSensorData);
        ConnectButtonText = "Connect";
        ConnectionStatus  = "Disconnected";
        LedButtonText  = "Turn LED On";
        LedButtonColor = Color.FromArgb("#2563EB");
        LedStatus      = "LED is Off";
        StreamButtonText = "Start Streaming";
    }

    [ObservableProperty] private double _temperature;
    [ObservableProperty] private double _humidity;

    [ObservableProperty] private string _ledButtonText;
    [ObservableProperty] private Color  _ledButtonColor;
    [ObservableProperty] private string _ledStatus;
    [ObservableProperty] private string _connectButtonText = string.Empty;
    [ObservableProperty] private string _connectionStatus = string.Empty;

    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _streamButtonText;

    private void OnSensorData(object? sender, (double temp, double humidity) data)
    {
        OnMain(() =>
        {
            Temperature = data.temp;
            Humidity = data.humidity;
        });
    }

    [RelayCommand]
    private async Task RefreshSensor()
    {
        (Temperature, Humidity) = await _ble.ReadDht22Async(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (!_isConnected)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            bool ok = await _ble.ConnectAsync(AppConstants.Devices.PiSensor, cts.Token);
            if (ok)
            {
                _isConnected = true;
                ConnectionStatus = "Connected";
                ConnectButtonText = "Disconnect";
            }
            else
            {
                ConnectionStatus = "Connection failed";
            }
        }
        else
        {
            if (IsStreaming)
                await ToggleStreaming();
            await _ble.DisconnectAsync();
            _isConnected = false;
            ConnectionStatus = "Disconnected";
            ConnectButtonText = "Connect";
        }
    }

    [RelayCommand]
    private async Task ToggleStreaming()
    {
        if (!IsStreaming)
        {
            await _ble.StartStreamingAsync(CancellationToken.None);
            IsStreaming = true;
            StreamButtonText = "Stop Streaming";
        }
        else
        {
            await _ble.StopStreamingAsync(CancellationToken.None);
            IsStreaming = false;
            StreamButtonText = "Start Streaming";
        }
    }

    [RelayCommand]
    private async Task ToggleLed()
    {
        bool isOn = await _ble.ToggleLedAsync();
        LedStatus      = isOn ? "LED is On" : "LED is Off";
        LedButtonText  = isOn ? "Turn LED Off" : "Turn LED On";
        LedButtonColor = isOn ? Color.FromArgb("#DC2626") : Color.FromArgb("#2563EB");
    }

    [RelayCommand]
    private Task NavigateBack() => Shell.Current.GoToAsync("..");
}
