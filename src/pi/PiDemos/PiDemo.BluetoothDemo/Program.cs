#nullable enable
using System;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;
using Iot.Device.DHTxx;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;

namespace PiBleDemo;

/// <summary>Encapsulates GPIO + sensor resources.</summary>
internal sealed class Hardware : IAsyncDisposable
{
    private readonly GpioController _gpio = new();
    private readonly Dht22 _dht;
    private readonly int _ledPin;

    public Hardware(int dhtPin = 4, int ledPin = 17)
    {
        _dht = new Dht22(dhtPin, useFullSampling: true);
        _ledPin = ledPin;
        _gpio.OpenPin(_ledPin, PinMode.Output);
        _gpio.Write(_ledPin, PinValue.Low);
    }

    public (double t, double h) ReadClimate()
    {
        if (!_dht.IsLastReadSuccessful) _dht.TryRead();
        return (_dht.Temperature.DegreesCelsius, _dht.Humidity);
    }

    public void SetLed(bool on) => _gpio.Write(_ledPin, on ? PinValue.High : PinValue.Low);

    public ValueTask DisposeAsync()
    {
        _gpio.Dispose();
        _dht.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>BLE GATT characteristic for temperature / humidity.</summary>
internal sealed class ClimateCharacteristic : GattLocalCharacteristic1
{
    private readonly Hardware _hw;

    public ClimateCharacteristic(Hardware hw)
        : base("e95d9250-251d-470a-a062-fa1922dfa9a8",
               GattCharacteristicFlags.Read | GattCharacteristicFlags.Notify,
               maxLength: 8)
    {
        _hw = hw;
    }

    protected override byte[] OnReadValue()
    {
        var (t, h) = _hw.ReadClimate();
        Span<byte> data = stackalloc byte[8];
        BitConverter.TryWriteBytes(data[..4], (float)t);
        BitConverter.TryWriteBytes(data.Slice(4, 4), (float)h);
        return data.ToArray();
    }

    public void PushNotification()
    {
        if (HasSubscribers)
            Notify(OnReadValue());
    }
}

/// <summary>Write-only LED characteristic (1 = on, 0 = off).</summary>
internal sealed class LedCharacteristic : GattLocalCharacteristic1
{
    private readonly Hardware _hw;

    public LedCharacteristic(Hardware hw)
        : base("e95d93ee-251d-470a-a062-fa1922dfa9a8",
               GattCharacteristicFlags.Write | GattCharacteristicFlags.WriteWithoutResponse,
               maxLength: 1)
    {
        _hw = hw;
    }

    protected override void OnWriteValue(ReadOnlySpan<byte> value, GattRequestState state)
    {
        if (value.Length == 1) _hw.SetLed(value[0] != 0);
    }
}

internal sealed class BleHost : IAsyncDisposable
{
    private readonly Hardware _hw;
    private readonly ClimateCharacteristic _climate;
    private readonly LedCharacteristic _led;
    private readonly IDisposable _advHandle;

    public BleHost(Hardware hw)
    {
        _hw = hw;
        _climate = new ClimateCharacteristic(hw);
        _led     = new LedCharacteristic(hw);

        var service = new GattLocalService1(
            "e95d93b0-251d-470a-a062-fa1922dfa9a8",
            _climate, _led);

        // Initialize BlueZ & register GATT service
        BlueZManager.Initialize();
        var adapter = BlueZManager.GetFirstAdapter();
        var gattMgr = adapter.GetGattManager();
        gattMgr.Register(service);

        // Advertise with a complete name + service UUID
        var advMgr = adapter.GetLEAdvertisingManager();
        var adv = new LEAdvertisement
        {
            Type = LEAdvertisingType.Peripheral,
            LocalName = "Pi-DHT-LED",
            ServiceUUIDs = { service.UUID }
        };
        _advHandle = advMgr.RegisterAdvertisement(adv);
        Console.WriteLine("BLE service started and advertising.");
    }

    public void Tick() => _climate.PushNotification();

    public ValueTask DisposeAsync()
    {
        _advHandle.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal static class Program
{
    static async Task Main()
    {
        using var hw  = new Hardware();
        await using var ble = new BleHost(hw);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; timer.Dispose(); };

        while (await timer.WaitForNextTickAsync())
            ble.Tick();

        Console.WriteLine("Shutting down cleanly.");
    }
}
