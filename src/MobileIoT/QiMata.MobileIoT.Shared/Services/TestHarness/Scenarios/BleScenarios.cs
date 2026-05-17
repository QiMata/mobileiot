using QiMata.MobileIoT.Shared.Services;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Shared.Services.TestHarness.Scenarios;

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

public sealed class BlePeripheralScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "ble-peripheral";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        IBleAdvertiseService advertiser;
        try { advertiser = Get<IBleAdvertiseService>(); }
        catch (InvalidOperationException) { return Skipped("BLE advertise not supported on this platform"); }

        if (!advertiser.IsAvailable)
            return Skipped("BLE advertise not available (no radio, disabled, or OEM restriction)");

        var serviceUuidStr = Arg(args, "serviceUuid", "");
        if (string.IsNullOrEmpty(serviceUuidStr) || !Guid.TryParse(serviceUuidStr, out var serviceUuid))
            return Skipped("missing or invalid serviceUuid");

        var payloadHex = Arg(args, "payloadHex", "");
        if (payloadHex.Length == 0)
            return Skipped("missing payloadHex");

        var payload = Convert.FromHexString(payloadHex);
        var deviceName = Arg(args, "deviceName", "MobileIotPeripheral");
        var durationMs = Math.Clamp(Arg(args, "durationMs", 15000), 500, 60000);

        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnWritten(object? sender, byte[] bytes) => tcs.TrySetResult(bytes);
        advertiser.CharacteristicWritten += OnWritten;

        try
        {
            try
            {
                await advertiser.StartAsync(serviceUuid, payload, deviceName, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                return Skipped($"BLE advertise start failed: {ex.Message}");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(durationMs);
            byte[]? written = null;
            try { written = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* timeout — falls through with written=null */ }

            return new Dictionary<string, object?>
            {
                ["served"] = written is not null,
                ["centralBytesReceived"] = written is null ? null : Convert.ToHexString(written),
                ["deviceName"] = deviceName,
                ["serviceUuid"] = serviceUuidStr
            };
        }
        finally
        {
            advertiser.CharacteristicWritten -= OnWritten;
            try { await advertiser.StopAsync().ConfigureAwait(false); }
            catch { /* best-effort cleanup */ }
        }
    }
}

public sealed class BleP2PCentralScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "ble-p2p-central";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        IBleP2PCentralService central;
        try { central = Get<IBleP2PCentralService>(); }
        catch (InvalidOperationException) { return Skipped("BLE P2P central not supported on this platform"); }

        var deviceName = Arg(args, "deviceName", "MobileIotPeripheral");
        var serviceUuidStr = Arg(args, "serviceUuid", "");
        if (string.IsNullOrEmpty(serviceUuidStr) || !Guid.TryParse(serviceUuidStr, out var serviceUuid))
            return Skipped("missing or invalid serviceUuid");

        var payloadHex = Arg(args, "payloadHex", "");
        if (payloadHex.Length == 0)
            return Skipped("missing payloadHex");

        var payload = Convert.FromHexString(payloadHex);
        var timeoutMs = Math.Clamp(Arg(args, "timeoutMs", 12000), 500, 60000);

        byte[]? response;
        try
        {
            response = await central.ConnectAndExchangeAsync(
                deviceName, serviceUuid, payload,
                TimeSpan.FromMilliseconds(timeoutMs),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["responseHex"] = null,
                ["reason"] = ex.Message
            };
        }

        return new Dictionary<string, object?>
        {
            ["ok"] = response is not null,
            ["responseHex"] = response is null ? null : Convert.ToHexString(response),
            ["reason"] = response is null ? "scan/connect/exchange timed out" : null
        };
    }
}
#endif
