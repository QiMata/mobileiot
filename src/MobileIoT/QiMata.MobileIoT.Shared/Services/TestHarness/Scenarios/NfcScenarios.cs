using QiMata.MobileIoT.Shared.Services;
#if TEST_HARNESS
using System.Text.Json;
using QiMata.MobileIoT.Shared.Models;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Shared.Services.TestHarness.Scenarios;

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
#endif
