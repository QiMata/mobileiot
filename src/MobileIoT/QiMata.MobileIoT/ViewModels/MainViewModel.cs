using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services.I;
using System.Threading;

namespace QiMata.MobileIoT.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothService _ble;

    [ObservableProperty] double tempC;
    [ObservableProperty] double humidity;

    public MainViewModel(IBluetoothService ble)
    {
        _ble = ble;
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
}
