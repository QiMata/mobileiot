using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.ViewModels;

public partial class AudioDemoViewModel : ObservableObject
{
    private readonly IAudioModemService _modem;

    [ObservableProperty]
    private string _status = "Idle";

    public AudioDemoViewModel(IAudioModemService modem)
    {
        _modem = modem;
        _modem.DataReceived += (_, msg) => Status = msg;
    }

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
