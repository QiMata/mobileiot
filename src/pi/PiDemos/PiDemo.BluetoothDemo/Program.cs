#nullable enable
using System;
using System.Device.Gpio;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Iot.Device.DHTxx;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Linux.Bluetooth.Gatt;

namespace PiBleDemo;

/// <summary>Encapsulates GPIO + sensor resources.</summary>
internal sealed class Hardware : IAsyncDisposable
{
    private readonly GpioController _gpio = new();
    private readonly Dht22 _dht;
    private readonly int _ledPin;

    public Hardware(int dhtPin = 4, int ledPin = 17)
    {
        _dht = new Dht22(dhtPin);
        _ledPin = ledPin;
        _gpio.OpenPin(_ledPin, PinMode.Output);
        _gpio.Write(_ledPin, PinValue.Low);
    }

    public (double t, double h) ReadClimate()
    {
        if (!_dht.TryReadTemperature(out var temp) ||
            !_dht.TryReadHumidity(out var hum))
            throw new IOException("Sensor read failed");

        return (temp.DegreesCelsius, hum.Percent);
    }

    public void SetLed(bool on) => _gpio.Write(_ledPin, on ? PinValue.High : PinValue.Low);

    public ValueTask DisposeAsync()
    {
        _gpio.Dispose();
        _dht.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>BLE GATT characteristic for temperature.</summary>
internal sealed class TemperatureCharacteristic
{
    private readonly Hardware _hw;
    public GattLocalCharacteristic Definition { get; }

    public TemperatureCharacteristic(Hardware hw)
    {
        _hw = hw;

        Definition = new GattLocalCharacteristicBuilder()
            .WithUuid("00002A6E-0000-1000-8000-00805F9B34FB")
            .WithFlags(
                GattCharacteristicFlag.Read,
                GattCharacteristicFlag.Notify)
            .WithReadHandler(_ => Task.FromResult(ReadTemperatureBytes()))
            .Build();
    }

    private byte[] ReadTemperatureBytes()
    {
        var (t, _) = _hw.ReadClimate();
        Span<byte> data = stackalloc byte[2];
        BitConverter.TryWriteBytes(data, (short)Math.Round(t * 100));
        return data.ToArray();
    }

    public async Task NotifyAsync(double t)
    {
        if (Definition.Subscribers.Any())
        {
            Span<byte> data = stackalloc byte[2];
            BitConverter.TryWriteBytes(data, (short)Math.Round(t * 100));
            await Definition.NotifyAsync(data.ToArray());
        }
    }
}

/// <summary>BLE GATT characteristic for humidity.</summary>
internal sealed class HumidityCharacteristic
{
    private readonly Hardware _hw;
    public GattLocalCharacteristic Definition { get; }

    public HumidityCharacteristic(Hardware hw)
    {
        _hw = hw;

        Definition = new GattLocalCharacteristicBuilder()
            .WithUuid("00002A6F-0000-1000-8000-00805F9B34FB")
            .WithFlags(
                GattCharacteristicFlag.Read,
                GattCharacteristicFlag.Notify)
            .WithReadHandler(_ => Task.FromResult(ReadHumidityBytes()))
            .Build();
    }

    private byte[] ReadHumidityBytes()
    {
        var (_, h) = _hw.ReadClimate();
        Span<byte> data = stackalloc byte[2];
        BitConverter.TryWriteBytes(data, (ushort)Math.Round(h * 100));
        return data.ToArray();
    }

    public async Task NotifyAsync(double h)
    {
        if (Definition.Subscribers.Any())
        {
            Span<byte> data = stackalloc byte[2];
            BitConverter.TryWriteBytes(data, (ushort)Math.Round(h * 100));
            await Definition.NotifyAsync(data.ToArray());
        }
    }
}

/// <summary>Write-only LED characteristic (1 = on, 0 = off).</summary>
internal sealed class LedCharacteristic
{
    private readonly Hardware _hw;
    public GattLocalCharacteristic Definition { get; }

    public LedCharacteristic(Hardware hw)
    {
        _hw = hw;

        Definition = new GattLocalCharacteristicBuilder()
            .WithUuid("12345679-1234-1234-1234-1234567890AB")
            .WithFlags(
                GattCharacteristicFlag.Write,
                GattCharacteristicFlag.WriteWithoutResponse)
            .WithWriteHandler((value, _) =>
            {
                if (value.Length == 1) _hw.SetLed(value[0] != 0);
                return Task.CompletedTask;
            })
            .Build();
    }
}

internal sealed class BleHost : IAsyncDisposable
{
    private readonly Hardware _hw;
    private readonly TemperatureCharacteristic _temp;
    private readonly HumidityCharacteristic _hum;
    private readonly LedCharacteristic _led;
    private readonly IAsyncDisposable _advHandle;

    private BleHost(Hardware hw, TemperatureCharacteristic temp, HumidityCharacteristic hum, LedCharacteristic led, IAsyncDisposable advHandle)
    {
        _hw = hw;
        _temp = temp;
        _hum = hum;
        _led = led;
        _advHandle = advHandle;
    }

    public static async Task<BleHost> StartAsync(Hardware hw)
    {
        var temp = new TemperatureCharacteristic(hw);
        var hum = new HumidityCharacteristic(hw);
        var led = new LedCharacteristic(hw);

        var service = new GattLocalServiceBuilder()
            .WithUuid("12345678-1234-1234-1234-1234567890AB")
            .AddCharacteristic(temp.Definition)
            .AddCharacteristic(hum.Definition)
            .AddCharacteristic(led.Definition)
            .Build();

        await Bluetooth.InitializeAsync();
        var adapter = await Bluetooth.GetDefaultAdapterAsync();
        var gatt = await adapter.GetGattServiceManagerAsync();
        await gatt.RegisterServiceAsync(service);

        var advertiser = await adapter.LeAdvertisingStartAsync(builder =>
        {
            builder
                .SetAdvertisementType(LEAdvertisementType.Peripheral)
                .SetLocalName("PiDHTSensor")
                .AddServiceUuid(service.Uuid);
        });

        Console.WriteLine("BLE service started and advertising.");
        return new BleHost(hw, temp, hum, led, advertiser);
    }

    public async Task TickAsync()
    {
        var pushTemp = _temp.Definition.Subscribers.Any();
        var pushHum  = _hum.Definition.Subscribers.Any();

        if (pushTemp || pushHum)
        {
            var (t, h) = _hw.ReadClimate();
            if (pushTemp)
                await _temp.NotifyAsync(t);
            if (pushHum)
                await _hum.NotifyAsync(h);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _advHandle.DisposeAsync();
    }
}

internal static class Program
{
    static async Task Main()
    {
        await using var hw  = new Hardware();
        await using var ble = await BleHost.StartAsync(hw);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; timer.Dispose(); };

        while (await timer.WaitForNextTickAsync())
            await ble.TickAsync();

        Console.WriteLine("Shutting down cleanly.");
    }
}
