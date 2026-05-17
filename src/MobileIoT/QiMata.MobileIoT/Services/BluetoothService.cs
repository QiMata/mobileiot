using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Extensions;
using QiMata.MobileIoT.Helpers;
using QiMata.MobileIoT.Shared.Helpers;
using QiMata.MobileIoT.Shared.Services.Interfaces;
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

    private static readonly BleUuidConfig.BleUuidValues Uuids = BleUuidConfig.Values;

    public event EventHandler<float>? TemperatureChanged;
    public event EventHandler<float>? HumidityChanged;

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
        var service = await _device.GetServiceAsync(Uuids.ServiceUuid, ct)
                      ?? throw new Exception("Service not found");

        _tempChar = await service.GetCharacteristicAsync(Uuids.TemperatureCharacteristicUuid);
        _humChar = await service.GetCharacteristicAsync(Uuids.HumidityCharacteristicUuid);
        _ledChar = await service.GetCharacteristicAsync(Uuids.LedCharacteristicUuid);

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

        var result = await _humChar.ReadAsync(ct);

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

    public async Task StartSensorNotificationsAsync(CancellationToken ct)
    {
        if (_tempChar is null || _humChar is null)
            throw new InvalidOperationException("Connect first");

        _tempChar.ValueUpdated += OnTempUpdated;
        await _tempChar.StartUpdatesAsync(ct);

        _humChar.ValueUpdated += OnHumUpdated;
        await _humChar.StartUpdatesAsync(ct);
    }

    public async Task StopSensorNotificationsAsync(CancellationToken ct)
    {
        if (_tempChar is not null)
        {
            _tempChar.ValueUpdated -= OnTempUpdated;
            await _tempChar.StopUpdatesAsync(ct);
        }
        if (_humChar is not null)
        {
            _humChar.ValueUpdated -= OnHumUpdated;
            await _humChar.StopUpdatesAsync(ct);
        }
    }

    private void OnTempUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var raw = BitConverter.ToInt16(e.Characteristic.Value, 0);
        TemperatureChanged?.Invoke(this, raw / 100.0f);
    }

    private void OnHumUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var raw = BitConverter.ToInt16(e.Characteristic.Value, 0);
        HumidityChanged?.Invoke(this, raw / 100.0f);
    }

    public async Task DisconnectAsync()
    {
        await StopSensorNotificationsAsync(CancellationToken.None);
        if (_device is not null && _adapt.ConnectedDevices.Contains(_device))
            await _adapt.DisconnectDeviceAsync(_device);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
