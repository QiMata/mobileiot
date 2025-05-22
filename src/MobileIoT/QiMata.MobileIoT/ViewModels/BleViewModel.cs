using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.ViewModels;

public partial class BleViewModel : ObservableObject
{
    private readonly IBleDemoService _ble;

    public BleViewModel(IBleDemoService ble)
    {
        _ble = ble;
        LedButtonText  = "Turn LED On";
        LedButtonColor = Color.FromArgb("#2563EB");
        LedStatus      = "LED is Off";
    }

    // --- DHT22 bindables ---
    [ObservableProperty] private double _temperature;
    [ObservableProperty] private double _humidity;

    // --- LED bindables ---
    [ObservableProperty] private string _ledButtonText;
    [ObservableProperty] private Color  _ledButtonColor;
    [ObservableProperty] private string _ledStatus;

    // --- Commands ---
    [RelayCommand]
    private async Task RefreshSensor()
    {
        (Temperature, Humidity) = await _ble.ReadDht22Async();
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
