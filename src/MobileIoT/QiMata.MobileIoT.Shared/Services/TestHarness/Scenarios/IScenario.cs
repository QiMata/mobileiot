#if TEST_HARNESS
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Services.TestHarness.Scenarios;

/// <summary>
/// A scenario is a scripted in-app flow the harness can start via POST /scenario/{name}.
/// Per-integration scenarios register by subclassing this and getting picked up by
/// <see cref="ScenarioRegistry"/> via DI. The framework PR defines the contract only;
/// per-integration scenario implementations arrive with their own plans.
/// </summary>
public interface IScenario
{
    string Name { get; }

    Task<IReadOnlyDictionary<string, object?>> StartAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object?>> GetStateAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
#endif
