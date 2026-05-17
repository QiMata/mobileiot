using QiMata.MobileIoT.Shared.Services;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Thread.Models;
using QiMata.MobileIoT.Shared.Thread.Services;

namespace QiMata.MobileIoT.Shared.Services.TestHarness.Scenarios;

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
#endif
