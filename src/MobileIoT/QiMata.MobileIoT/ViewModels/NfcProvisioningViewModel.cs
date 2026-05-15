using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Models;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class NfcProvisioningViewModel : BaseViewModel
{
    private readonly INfcService _nfc;

    public NfcProvisioningViewModel(INfcService nfc, IAppLogger logger) : base(logger)
    {
        _nfc = nfc;
    }

    [ObservableProperty] string _wifiSsid = string.Empty;
    [ObservableProperty] string _wifiPassword = string.Empty;
    [ObservableProperty] string _wifiSecurity = "WPA2";
    [ObservableProperty] string _deviceName = string.Empty;
    [ObservableProperty] string _mqttBroker = string.Empty;
    [ObservableProperty] int _mqttPort = 1883;
    [ObservableProperty] string _provisionStatus = "Ready";

    public List<string> SecurityOptions { get; } = ["WPA2", "WPA3", "Open"];

    [RelayCommand]
    async Task WriteProvisioningTagAsync()
    {
        var config = new ProvisioningConfig
        {
            WifiSsid = WifiSsid,
            WifiPass = WifiPassword,
            WifiSecurity = WifiSecurity,
            DeviceName = DeviceName,
            MqttBroker = MqttBroker,
            MqttPort = MqttPort
        };

        string json = JsonSerializer.Serialize(config);
        ProvisionStatus = "Tap NFC tag or Pi NFC reader...";

        try
        {
            await _nfc.WriteTextAsync(json);
            ProvisionStatus = "Provisioning data written!";
        }
        catch (Exception ex)
        {
            ProvisionStatus = $"Write failed: {ex.Message}";
            Logger.Warn("NFC provisioning write failed", ex);
        }
    }

    [RelayCommand]
    Task NavigateBack() => Shell.Current.GoToAsync("..");
}
