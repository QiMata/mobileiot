using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class UsbViewModel : BaseViewModel
{
    readonly IUsbDeviceService _usb;

    public UsbViewModel(IUsbDeviceService usb, IAppLogger logger) : base(logger)
    {
        _usb = usb;
    }

    [ObservableProperty]
    private string _log = string.Empty;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var devices = await _usb.ListAsync();
        if (devices.Any() && await _usb.OpenAsync(devices[0].VendorId, devices[0].ProductId))
        {
            var displayName = string.IsNullOrWhiteSpace(devices[0].Name)
                ? devices[0].Identifier
                : devices[0].Name;
            Log += $"Connected to {displayName}\n";
        }
        else
            Log += "No device or failed to open.\n";
    }

    [RelayCommand]
    private async Task SendPingAsync()
    {
        if (!_usb.IsOpen)
            return;
        await _usb.WriteAsync(BleConstants.Ping);
        var buf = new byte[64];
        int n = await _usb.ReadAsync(buf);
        if (n > 0)
            Log += $"RX {n} bytes\n";
    }
}
