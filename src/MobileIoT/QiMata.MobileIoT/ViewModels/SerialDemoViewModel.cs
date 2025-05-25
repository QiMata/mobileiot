using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Services;
using System.Text;

namespace QiMata.MobileIoT.ViewModels;

public partial class SerialDemoViewModel : ObservableObject
{
    private readonly ISerialDeviceService _serial;

    [ObservableProperty]
    string _log = string.Empty;

    public SerialDemoViewModel(ISerialDeviceService serial)
    {
        _serial = serial;
        _serial.DataReceived += (_, mem) =>
            Log += $"RX: {Encoding.ASCII.GetString(mem.Span)}{Environment.NewLine}";
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var devices = await _serial.ListAsync();
        if (devices.Count == 0) { Log += "No devices found\n"; return; }

        var d = devices[0];
        bool ok = await _serial.OpenAsync(d.VendorId, d.ProductId, 9600);
        Log += ok ? "Connected\n" : "Open failed\n";
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!_serial.IsOpen) { Log += "Not open\n"; return; }
        await _serial.WriteAsync(Encoding.ASCII.GetBytes("LED_ON\n"));
        Log += "TX: LED_ON\n";
    }
}
