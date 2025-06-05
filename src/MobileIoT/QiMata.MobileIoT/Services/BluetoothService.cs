using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Extensions;
using QiMata.MobileIoT.Helpers;
using QiMata.MobileIoT.Services.I;
using System;
using System.Threading;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.EventArgs;

namespace QiMata.MobileIoT.Services;

public sealed class BluetoothService : IBluetoothService, IAsyncDisposable
{
    private readonly IBluetoothLE _ble = CrossBluetoothLE.Current;
    private readonly IAdapter _adapt = CrossBluetoothLE.Current.Adapter;

    private IDevice? _device;
    private ICharacteristic? _tempChar, _humChar, _ledChar;

    private static readonly Guid ServiceUuid = Guid.Parse("12345678-1234-1234-1234-1234567890AB");
    private static readonly Guid TempUuid = Guid.Parse("00002A6E-0000-1000-8000-00805F9B34FB");
    private static readonly Guid HumUuid = Guid.Parse("00002A6F-0000-1000-8000-00805F9B34FB");
    private static readonly Guid LedUuid = Guid.Parse("12345679-1234-1234-1234-1234567890AB");

    public async Task<bool> ConnectAsync(string advertisedName, CancellationToken ct)
    {
        await BlePermissions.EnsureAsync();

        var tcs = new TaskCompletionSource<IDevice?>();
        void handler(object? _, DeviceEventArgs a)
        {
            if (a.Device.Name == advertisedName)
                tcs.TrySetResult(a.Device);
        }

        _adapt.DeviceDiscovered += handler;
        try
        {
            await _adapt.StartScanningForDevicesAsync(ct);
            _device = await tcs.Task.WaitAsync(ct);
        }
        finally
        {
            _adapt.DeviceDiscovered -= handler;
        }

        if (_device is null) return false;

        await _adapt.ConnectToDeviceAsync(_device, cancellationToken: ct);
        var service = await _device.GetServiceAsync(ServiceUuid, ct)
                      ?? throw new Exception("Service not found");

        _tempChar = await service.GetCharacteristicAsync(TempUuid);
        _humChar = await service.GetCharacteristicAsync(HumUuid);
        _ledChar = await service.GetCharacteristicAsync(LedUuid);

        return _tempChar != null && _humChar != null && _ledChar != null;
    }

    public async Task<float> ReadTemperatureAsync(CancellationToken ct)
    {
        if (_tempChar is null) throw new InvalidOperationException("Connect first");

        var result = await _tempChar.ReadAsync(ct);

        if (result.resultCode != 0)
        {
            return 0f;
        }

        var raw = BitConverter.ToInt16(result.data, 0);
        return  raw / 100.0f;
    }

    public async Task<float> ReadHumidityAsync(CancellationToken ct)
    {
        if (_humChar is null) throw new InvalidOperationException("Connect first");

        var result = await _tempChar.ReadAsync(ct);

        if (result.resultCode != 0)
        {
            return 0f;
        }

        var raw = BitConverter.ToInt16(result.data, 0);
        return raw / 100.0f;
    }

    public Task ToggleLedAsync(bool on, CancellationToken ct) =>
        _ledChar?.WriteAsync(new[] { (byte)(on ? 1 : 0) }, ct)
        ?? throw new InvalidOperationException("Connect first");

    public async Task DisconnectAsync()
    {
        if (_device is not null && _adapt.ConnectedDevices.Contains(_device))
            await _adapt.DisconnectDeviceAsync(_device);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
