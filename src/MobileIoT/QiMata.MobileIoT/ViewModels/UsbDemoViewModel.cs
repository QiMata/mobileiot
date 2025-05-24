using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.ViewModels;

public partial class UsbDemoViewModel : ObservableObject
{
    readonly IUsbSerialPort _usb;

    public UsbDemoViewModel(IUsbSerialPort usb)
        => _usb = usb;

    [ObservableProperty]
    private string _log = string.Empty;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var devices = await _usb.ListDevicesAsync();
        if (devices.Any() && await _usb.OpenAsync(devices[0].VendorId, devices[0].ProductId))
            Log += $"Connected to {devices[0].Name}\n";
        else
            Log += "No device or failed to open.\n";
    }

    [RelayCommand]
    private async Task SendPingAsync()
    {
        if (!_usb.IsOpen)
            return;
        await _usb.WriteAsync(new byte[] { 0x50, 0x49, 0x4E, 0x47 });
        var buf = new byte[64];
        int n = await _usb.ReadAsync(buf);
        if (n > 0)
            Log += $"RX {n} bytes\n";
    }
}
