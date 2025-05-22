using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using QiMata.MobileIoT.Helpers;
using QiMata.MobileIoT.Services.I;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services;

public sealed class BluetoothService : IBluetoothService
{
    private readonly IBluetoothLE _ble = CrossBluetoothLE.Current;
    private readonly IAdapter _adapt = CrossBluetoothLE.Current.Adapter;

    private IDevice? _device;
    private ICharacteristic? _tempChar, _humChar, _ledChar;

    private static readonly Guid ServiceUuid = Guid.Parse("12345678-1234-1234-1234-1234567890AB");
    private static readonly Guid TempUuid = Guid.Parse("00002A6E-0000-1000-8000-00805F9B34FB");
    private static readonly Guid HumUuid = Guid.Parse("00002A6F-0000-1000-8000-00805F9B34FB");
    private static readonly Guid LedUuid = Guid.Parse("12345679-1234-1234-1234-1234567890AB");

    public event EventHandler<double>? TemperatureUpdatedC;
    public event EventHandler<double>? HumidityUpdatedPercent;

    public async Task<bool> ConnectAsync(string advertisedName, CancellationToken ct)
    {
        await BlePermissions.EnsureAsync();

        TaskCompletionSource<IDevice?> tcs = new();
        void handler(object? s, DeviceEventArgs a)
        {
            if (a.Device.Name == advertisedName)
                tcs.TrySetResult(a.Device);
        }
        _adapt.DeviceDiscovered += handler;
        await _adapt.StartScanningForDevicesAsync(ct);
        _device = await tcs.Task.WaitAsync(ct);
        _adapt.DeviceDiscovered -= handler;
        if (_device is null) return false;

        await _adapt.ConnectToDeviceAsync(_device, ct: ct);
        var service = await _device.GetServiceAsync(ServiceUuid);
        if (service is null) throw new Exception("Service not found");

        _tempChar = await service.GetCharacteristicAsync(TempUuid);
        _humChar = await service.GetCharacteristicAsync(HumUuid);
        _ledChar = await service.GetCharacteristicAsync(LedUuid);

        return _tempChar != null && _humChar != null && _ledChar != null;
    }

    public async Task StartSensorNotificationsAsync(CancellationToken ct)
    {
        if (_tempChar is null || _humChar is null)
            throw new InvalidOperationException("Connect first");

        _tempChar.ValueUpdated += (s, e) =>
        {
            var raw = BitConverter.ToInt16(e.Characteristic.Value, 0);
            TemperatureUpdatedC?.Invoke(this, raw / 100.0);
        };
        _humChar.ValueUpdated += (s, e) =>
        {
            var raw = BitConverter.ToUInt16(e.Characteristic.Value, 0);
            HumidityUpdatedPercent?.Invoke(this, raw / 100.0);
        };

        await _tempChar.StartUpdatesAsync();
        await _humChar.StartUpdatesAsync();
    }

    public Task ToggleLedAsync(bool on, CancellationToken ct)
        => _ledChar?.WriteAsync(new[] { (byte)(on ? 1 : 0) }, ct)
           ?? throw new InvalidOperationException("Connect first");

    public async Task DisconnectAsync()
    {
        if (_device is not null && _adapt.ConnectedDevices.Contains(_device))
            await _adapt.DisconnectDeviceAsync(_device);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
