#if TEST_HARNESS
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace QiMata.MobileIoT.Services.TestHarness.Scenarios;

public abstract class ScenarioBase : IScenario
{
    private readonly object _sync = new();
    private Dictionary<string, object?> _state = new() { ["status"] = "idle" };

    protected ScenarioBase(IServiceProvider services)
    {
        Services = services;
    }

    protected IServiceProvider Services { get; }

    public abstract string Name { get; }

    public async Task<IReadOnlyDictionary<string, object?>> StartAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        SetState("running");

        try
        {
            var result = await ExecuteAsync(args, cancellationToken).ConfigureAwait(false);
            if (result.TryGetValue("status", out var explicitStatus))
            {
                SetState(explicitStatus?.ToString() ?? "passed", result);
            }
            else
            {
                SetState("passed", result);
            }
        }
        catch (PlatformNotSupportedException ex)
        {
            SetState("skipped", new Dictionary<string, object?> { ["reason"] = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            SetState("skipped", new Dictionary<string, object?> { ["reason"] = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No service for type", StringComparison.OrdinalIgnoreCase))
        {
            SetState("skipped", new Dictionary<string, object?> { ["reason"] = ex.Message });
        }
        catch (Exception ex)
        {
            SetState("failed", new Dictionary<string, object?>
            {
                ["error"] = ex.Message,
                ["errorType"] = ex.GetType().Name
            });
        }

        return await GetStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyDictionary<string, object?>> GetStateAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>(_state));
        }
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        SetState("skipped", new Dictionary<string, object?> { ["reason"] = "Scenario stopped before completion" });
        return Task.CompletedTask;
    }

    protected abstract Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken);

    protected T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    protected static string Arg(IReadOnlyDictionary<string, object?> args, string name, string fallback)
    {
        if (!args.TryGetValue(name, out var value) || value is null)
            return fallback;

        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? fallback,
            JsonElement e => e.ToString(),
            _ => value.ToString() ?? fallback
        };
    }

    protected static int Arg(IReadOnlyDictionary<string, object?> args, string name, int fallback)
    {
        if (!args.TryGetValue(name, out var value) || value is null)
            return fallback;

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            JsonElement { ValueKind: JsonValueKind.Number } e when e.TryGetInt32(out var i) => i,
            string s when int.TryParse(s, out var i) => i,
            _ => fallback
        };
    }

    protected static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    protected static IReadOnlyDictionary<string, object?> Skipped(string reason) => new Dictionary<string, object?>
    {
        ["status"] = "skipped",
        ["reason"] = reason
    };

    private void SetState(string status, IReadOnlyDictionary<string, object?>? values = null)
    {
        lock (_sync)
        {
            _state = new Dictionary<string, object?>(values ?? new Dictionary<string, object?>())
            {
                ["status"] = status,
                ["updatedUtc"] = DateTimeOffset.UtcNow
            };
        }
    }
}
#endif
