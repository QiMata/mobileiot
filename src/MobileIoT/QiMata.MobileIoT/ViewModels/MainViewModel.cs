using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services.I;
using QiMata.MobileIoT.Services;
using System.Threading;

namespace QiMata.MobileIoT.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothService _ble;
    private readonly IQrScanningService _qrScanner;

    [ObservableProperty] double tempC;
    [ObservableProperty] double humidity;

    public MainViewModel(IBluetoothService ble, IQrScanningService qrScanner)
    {
        _ble = ble;
        _qrScanner = qrScanner;
        _ble.TemperatureUpdatedC += (_, v) => TempC = v;
        _ble.HumidityUpdatedPercent += (_, v) => Humidity = v;
    }

    [RelayCommand]
    private async Task InitAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        bool ok = await _ble.ConnectAsync("PiDHTSensor", cts.Token);
        if (ok) await _ble.StartSensorNotificationsAsync(cts.Token);
    }

    [RelayCommand]
    private Task ToggleLedAsync(bool on)
        => _ble.ToggleLedAsync(on, CancellationToken.None);

    [RelayCommand]
    private async Task ScanQrAsync()
    {
        var result = await _qrScanner.ScanAsync();
        // handle the result as needed
    }
}
