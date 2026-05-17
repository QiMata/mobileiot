using QiMata.MobileIoT.Shared.Services;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Thread.Services;

namespace QiMata.MobileIoT.Shared.Services.TestHarness.Scenarios;

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
