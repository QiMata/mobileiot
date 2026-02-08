using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services.Interfaces;
using System;
using System.Threading;

namespace QiMata.MobileIoT.ViewModels;

public partial class BleViewModel : ObservableObject
{
    private readonly IBleDemoService _ble;
    private bool _isConnected;

    public BleViewModel(IBleDemoService ble)
    {
        _ble = ble;
        _ble.SensorDataReceived += OnSensorData;
        ConnectButtonText = "Connect";
        ConnectionStatus  = "Disconnected";
        LedButtonText  = "Turn LED On";
        LedButtonColor = Color.FromArgb("#2563EB");
        LedStatus      = "LED is Off";
        StreamButtonText = "Start Streaming";
    }

    // --- DHT22 bindables ---
    [ObservableProperty] private double _temperature;
    [ObservableProperty] private double _humidity;

    // --- LED bindables ---
    [ObservableProperty] private string _ledButtonText;
    [ObservableProperty] private Color  _ledButtonColor;
    [ObservableProperty] private string _ledStatus;
    [ObservableProperty] private string _connectButtonText = string.Empty;
    [ObservableProperty] private string _connectionStatus = string.Empty;

    // --- Streaming bindables ---
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _streamButtonText;

    private void OnSensorData(object? sender, (double temp, double humidity) data)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Temperature = data.temp;
            Humidity = data.humidity;
        });
    }

    // --- Commands ---
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
            bool ok = await _ble.ConnectAsync("PiSensor", cts.Token);
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
