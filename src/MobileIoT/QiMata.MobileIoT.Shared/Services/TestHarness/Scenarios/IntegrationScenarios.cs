#if TEST_HARNESS
using System.Text.Json;
using QiMata.MobileIoT.Models;
using QiMata.MobileIoT.Services.Interfaces;
using QiMata.MobileIoT.ThreadDemoCore.Models;
using QiMata.MobileIoT.ThreadDemoCore.Services;
using QiMata.MobileIoT.Usb;

namespace QiMata.MobileIoT.Services.TestHarness.Scenarios;

public sealed class BleGattScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "ble-gatt";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var service = Get<IBleDemoService>();
        var deviceName = Arg(args, "deviceName", "PiDHTSensor");
        var connected = await service.ConnectAsync(deviceName, cancellationToken).ConfigureAwait(false);
        if (!connected)
            return Skipped($"BLE device '{deviceName}' not found");

        try
        {
            var reading = await service.ReadDht22Async(cancellationToken).ConfigureAwait(false);
            var led = await service.ToggleLedAsync().ConfigureAwait(false);
            return new Dictionary<string, object?>
            {
                ["deviceName"] = deviceName,
                ["temperature"] = reading.temp,
                ["humidity"] = reading.humidity,
                ["led"] = led
            };
        }
        finally
        {
            await service.DisconnectAsync().ConfigureAwait(false);
        }
    }
}

public sealed class BeaconScanScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "ble-beacon";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var scanner = Get<IBeaconScanner>();
        var durationMs = Math.Clamp(Arg(args, "durationMs", 1500), 100, 10000);
        var seen = new List<BeaconAdvertisement>();

        void OnAdvertisement(object? sender, BeaconAdvertisement advertisement) => seen.Add(advertisement);

        scanner.AdvertisementReceived += OnAdvertisement;
        try
        {
            scanner.StartScanning();
            await Task.Delay(durationMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scanner.AdvertisementReceived -= OnAdvertisement;
            scanner.StopScanning();
        }

        return new Dictionary<string, object?>
        {
            ["count"] = seen.Count,
            ["devices"] = seen.Take(10).Select(a => new
            {
                a.DeviceId,
                a.Name,
                a.Rssi,
                DataLength = a.Data.Length
            }).ToArray()
        };
    }
}

public sealed class NfcTagScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "nfc-tag";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var nfc = Get<INfcService>();
        if (!nfc.IsAvailable)
            return Skipped("NFC is not available on this platform/device");
        if (!nfc.IsEnabled)
            return Skipped("NFC is disabled");

        var text = Arg(args, "text", "MobileIoT harness");
        string? received = null;
        void OnMessage(object? sender, string message) => received = message;

        nfc.MessageReceived += OnMessage;
        try
        {
            await nfc.StartListeningAsync().ConfigureAwait(false);
            await nfc.WriteTextAsync(text).ConfigureAwait(false);
            await Task.Delay(Math.Clamp(Arg(args, "waitMs", 500), 50, 5000), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            nfc.MessageReceived -= OnMessage;
            await nfc.StopListeningAsync().ConfigureAwait(false);
        }

        return new Dictionary<string, object?> { ["written"] = text, ["received"] = received };
    }
}

public sealed class NfcP2PScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "nfc-p2p";

    protected override Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var p2p = Get<INfcP2PService>();
        var text = Arg(args, "text", "MobileIoT peer message");
        p2p.StartP2P(text);
        p2p.StopP2P();
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>
        {
            ["sent"] = text
        });
    }
}

public sealed class NfcProvisioningScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "nfc-provisioning";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var nfc = Get<INfcService>();
        if (!nfc.IsAvailable)
            return Skipped("NFC is not available on this platform/device");
        if (!nfc.IsEnabled)
            return Skipped("NFC is disabled");

        var config = new ProvisioningConfig
        {
            WifiSsid = Arg(args, "wifiSsid", "MobileIoT"),
            WifiPass = Arg(args, "wifiPass", "change-me"),
            DeviceName = Arg(args, "deviceName", "miot-device"),
            MqttBroker = Arg(args, "mqttBroker", "mqtt.local")
        };
        var json = JsonSerializer.Serialize(config);
        await nfc.WriteTextAsync(json).ConfigureAwait(false);
        return new Dictionary<string, object?> { ["payload"] = json };
    }
}

public sealed class NfcHceEmulateScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "nfc-hce-emulate";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        IHceService hce;
        try { hce = Get<IHceService>(); }
        catch (InvalidOperationException) { return Skipped("HCE not supported on this platform"); }

        if (!hce.IsAvailable)
            return Skipped("HCE not available (no NFC radio or unsupported)");

        var aidHex = Arg(args, "aidHex", "F0010203040506");
        var payloadHex = Arg(args, "payloadHex", "");
        if (payloadHex.Length == 0)
            return Skipped("missing payloadHex");

        var aid = Convert.FromHexString(aidHex);
        var payload = Convert.FromHexString(payloadHex);
        var durationMs = Math.Clamp(Arg(args, "durationMs", 8000), 500, 60000);

        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnServed(object? sender, byte[] bytes) => tcs.TrySetResult(bytes);
        hce.PayloadServed += OnServed;
        await hce.StartAsync(aid, payload, cancellationToken).ConfigureAwait(false);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(durationMs);
            byte[]? served = null;
            try { served = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* timeout */ }

            return new Dictionary<string, object?>
            {
                ["aidHex"] = aidHex,
                ["served"] = served is not null,
                ["servedPayloadHex"] = served is null ? null : Convert.ToHexString(served),
                ["selectedAidHex"] = hce.LastSelectedAid is null ? null : Convert.ToHexString(hce.LastSelectedAid)
            };
        }
        finally
        {
            hce.PayloadServed -= OnServed;
            await hce.StopAsync().ConfigureAwait(false);
        }
    }
}

public sealed class NfcReaderScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "nfc-reader";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        INfcReaderService reader;
        try { reader = Get<INfcReaderService>(); }
        catch (InvalidOperationException) { return Skipped("NFC reader-mode not supported on this platform"); }

        if (!reader.IsAvailable)
            return Skipped("NFC reader-mode not available (no NFC radio or no foreground activity)");

        var aidHex = Arg(args, "aidHex", "F0010203040506");
        var timeoutMs = Math.Clamp(Arg(args, "timeoutMs", 8000), 500, 60000);
        var aid = Convert.FromHexString(aidHex);

        var response = await reader.ReadOnceAsync(aid, TimeSpan.FromMilliseconds(timeoutMs), cancellationToken)
            .ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["aidHex"] = aidHex,
            ["ok"] = response is not null,
            ["responseHex"] = response is null ? null : Convert.ToHexString(response)
        };
    }
}

public sealed class WifiDirectScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "wifi-direct";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var p2p = Get<IP2PService>();
        var discovered = await p2p.StartDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var sent = await p2p.SendAsync(Bytes(Arg(args, "message", "ping")), null, cancellationToken).ConfigureAwait(false);
        await p2p.StopAsync().ConfigureAwait(false);
        return new Dictionary<string, object?> { ["discoveryStarted"] = discovered, ["sent"] = sent };
    }
}

public sealed class UsbBulkScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "usb-bulk";

    protected override Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var usb = Get<IUsbCommunicator>();
        var devices = usb.ListDevices().ToArray();
        if (devices.Length == 0)
            return Task.FromResult(Skipped("No USB bulk devices found"));

        var target = devices[0];
        if (!usb.OpenDevice(target.Identifier))
            return Task.FromResult(Skipped($"Failed to open USB device '{target.Identifier}'"));

        var tx = Bytes(Arg(args, "message", "PING"));
        var written = usb.Write(tx);
        var rx = new byte[Math.Max(64, tx.Length)];
        var read = usb.Read(rx);
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>
        {
            ["device"] = target.Identifier,
            ["written"] = written,
            ["read"] = read,
            ["readHex"] = Convert.ToHexString(rx, 0, Math.Max(0, read))
        });
    }
}

public sealed class UsbSerialScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "usb-serial";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var serial = Get<ISerialDeviceService>();
        var devices = await serial.ListAsync(cancellationToken).ConfigureAwait(false);
        if (devices.Count == 0)
            return Skipped("No USB serial devices found");

        var target = devices[0];
        var opened = await serial.OpenAsync(target.VendorId, target.ProductId, 9600, cancellationToken).ConfigureAwait(false);
        if (!opened)
            return Skipped($"Failed to open serial device '{target.ProductName}'");

        var command = Arg(args, "command", "LED_ON\n");
        var written = await serial.WriteAsync(Bytes(command), cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["device"] = target.ProductName,
            ["written"] = written
        };
    }
}

public sealed class VisionScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "vision";

    protected override Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var imagePath = Arg(args, "imagePath", string.Empty);
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return Task.FromResult(Skipped("Vision scenario requires an imagePath argument that exists on the app host"));

        using var stream = File.OpenRead(imagePath);
        var label = Get<IImageClassificationService>().ClassifyImage(stream);
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>
        {
            ["label"] = label
        });
    }
}

public sealed class AudioModemScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "audio-modem";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var audio = Get<IAudioModemService>();
        var waitMs = Math.Clamp(Arg(args, "waitMs", 500), 50, 5000);
        string? message = null;
        void OnData(object? sender, string data) => message = data;

        audio.DataReceived += OnData;
        try
        {
            await audio.StartAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            audio.DataReceived -= OnData;
            await audio.StopAsync().ConfigureAwait(false);
        }

        return new Dictionary<string, object?> { ["message"] = message };
    }
}

public sealed class ThreadScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "thread";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var service = Get<IThreadDemoService>();
        var settings = new ThreadConnectionSettings
        {
            UseLiveBridge = false,
            BridgeBaseUrl = Arg(args, "bridgeUrl", "http://raspberrypi.local:8080"),
            TargetNode = Arg(args, "targetNode", string.Empty),
            TimeoutMs = Arg(args, "timeoutMs", 3000)
        };
        var status = await service.GetStatusAsync(settings, cancellationToken).ConfigureAwait(false);
        var ping = await service.SendPingAsync(settings, Arg(args, "payload", "ping"), cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["role"] = status.Role,
            ["sourceMode"] = status.SourceMode,
            ["pingSuccess"] = ping.Success,
            ["pingResponse"] = ping.ResponsePayload
        };
    }
}

public sealed class PiCameraScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "pi-camera";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var pi = Get<IPiCameraService>();
        var address = Arg(args, "piAddress", "raspberrypi.local");
        var healthy = await pi.CheckHealthAsync(address, cancellationToken).ConfigureAwait(false);
        if (!healthy)
            return Skipped($"Pi camera service at '{address}' is not reachable");

        var classification = await pi.ClassifyRemoteAsync(address, cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["piAddress"] = address,
            ["healthy"] = healthy,
            ["label"] = classification?.label,
            ["confidence"] = classification?.confidence
        };
    }
}
#endif
