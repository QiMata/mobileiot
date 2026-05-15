using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.Interfaces;
using System.Text;

namespace QiMata.MobileIoT.ViewModels;

public partial class SerialDemoViewModel : BaseViewModel
{
    private readonly ISerialDeviceService _serial;

    [ObservableProperty]
    string _log = string.Empty;

    [ObservableProperty]
    string _connectionStatus = "Disconnected";

    [ObservableProperty]
    string _connectButtonText = "Connect";

    [ObservableProperty]
    string _gpioPin = "17";

    [ObservableProperty]
    string _gpioValue = "1";

    [ObservableProperty]
    string _lastSensorTemp = "--";

    [ObservableProperty]
    string _lastSensorHum = "--";

    [ObservableProperty]
    string _piStatus = "--";

    public SerialDemoViewModel(ISerialDeviceService serial, IAppLogger logger) : base(logger)
    {
        _serial = serial;
        Subscribe<ReadOnlyMemory<byte>>(
            h => _serial.DataReceived += h,
            h => _serial.DataReceived -= h,
            OnDataReceived);
    }

    private string _rxBuffer = string.Empty;

    private void OnDataReceived(object? sender, ReadOnlyMemory<byte> mem)
    {
        _rxBuffer += Encoding.ASCII.GetString(mem.Span);

        while (_rxBuffer.Contains('\n'))
        {
            int idx = _rxBuffer.IndexOf('\n');
            string line = _rxBuffer[..idx].Trim();
            _rxBuffer = _rxBuffer[(idx + 1)..];

            if (string.IsNullOrEmpty(line))
                continue;

            OnMain(() => ParseResponse(line));
        }
    }

    private void ParseResponse(string line)
    {
        if (line.StartsWith("SENSOR:"))
        {
            var payload = line["SENSOR:".Length..];
            foreach (var pair in payload.Split(','))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    if (kv[0] == "temp") LastSensorTemp = kv[1];
                    else if (kv[0] == "hum") LastSensorHum = kv[1];
                }
            }
            AppendLine($"RX: {line}");
        }
        else if (line.StartsWith("STATUS:"))
        {
            PiStatus = line["STATUS:".Length..].Replace(",", "  |  ");
            AppendLine($"RX: {line}");
        }
        else
        {
            AppendLine($"RX: {line}");
        }
    }

    private void AppendLine(string message)
    {
        Log += message + "\n";
        Logger.Info(message);
    }

    private async Task SendCommandAsync(string cmd)
    {
        if (!_serial.IsOpen)
        {
            AppendLine("Not connected");
            return;
        }
        await _serial.WriteAsync(Encoding.ASCII.GetBytes($"{cmd}\n"));
        AppendLine($"TX: {cmd}");
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (_serial.IsOpen)
        {
            await _serial.DisposeAsync();
            ConnectionStatus = "Disconnected";
            ConnectButtonText = "Connect";
            return;
        }

        var devices = await _serial.ListAsync();
        if (devices.Count == 0)
        {
            AppendLine("No devices found");
            return;
        }

        var d = devices[0];
        bool ok = await _serial.OpenAsync(d.VendorId, d.ProductId, TransportConstants.UsbDefaultBaud);
        if (ok)
        {
            ConnectionStatus = $"Connected ({d.ProductName})";
            ConnectButtonText = "Disconnect";
        }
        else
        {
            ConnectionStatus = "Open failed";
        }
        AppendLine(ok ? "Connected" : "Open failed");
    }

    [RelayCommand]
    private Task SendLedOnAsync() => SendCommandAsync("LED_ON");

    [RelayCommand]
    private Task SendLedOffAsync() => SendCommandAsync("LED_OFF");

    [RelayCommand]
    private Task SendGpioReadAsync() => SendCommandAsync($"GPIO_READ {GpioPin}");

    [RelayCommand]
    private Task SendGpioWriteAsync() => SendCommandAsync($"GPIO_WRITE {GpioPin} {GpioValue}");

    [RelayCommand]
    private Task SendSensorReadAsync() => SendCommandAsync("SENSOR_READ");

    [RelayCommand]
    private Task SendStatusAsync() => SendCommandAsync("STATUS");

    [RelayCommand]
    private Task NavigateBack() => Shell.Current.GoToAsync("..");
}
