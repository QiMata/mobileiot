using QiMata.MobileIoT.Shared.Services;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Shared.Services.TestHarness.Scenarios;

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
#endif
