using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class AudioDemoViewModel : BaseViewModel
{
    private readonly IAudioModemService _modem;

    [ObservableProperty]
    private string _status = "Idle";

    public AudioDemoViewModel(IAudioModemService modem, IAppLogger logger) : base(logger)
    {
        _modem = modem;
        Subscribe<string>(
            h => _modem.DataReceived += h,
            h => _modem.DataReceived -= h,
            OnDataReceived);
    }

    private void OnDataReceived(object? sender, string msg) => Status = msg;

    [RelayCommand]
    private async Task StartAsync()
    {
        Status = "Listening...";
        await _modem.StartAsync();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _modem.StopAsync();
        Status = "Stopped";
    }

    [RelayCommand]
    private Task NavigateBack() => Shell.Current.GoToAsync("..");
}
