using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System.Threading;

namespace QiMata.MobileIoT.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly IBluetoothService _ble;
    private readonly IQrScanningService _qrScanner;

    [ObservableProperty] double tempC;
    [ObservableProperty] double humidity;
    [ObservableProperty] string _lastQrResult = string.Empty;

    public MainViewModel(IBluetoothService ble, IQrScanningService qrScanner, IAppLogger logger) : base(logger)
    {
        _ble = ble;
        _qrScanner = qrScanner;
    }

    [RelayCommand]
    private async Task InitAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        bool ok = await _ble.ConnectAsync(AppConstants.Devices.PiDHTSensor, cts.Token);
        Logger.Info(ok ? "Connected to DHT sensor" : "DHT sensor connect failed");
    }

    [RelayCommand]
    private Task ToggleLedAsync(bool on)
        => _ble.ToggleLedAsync(on, CancellationToken.None);

    [RelayCommand]
    private async Task ScanQrAsync()
    {
        var result = await _qrScanner.ScanAsync();
        if (!string.IsNullOrEmpty(result))
        {
            LastQrResult = result;
            Logger.Info($"QR scanned: {result}");
        }
    }
}
