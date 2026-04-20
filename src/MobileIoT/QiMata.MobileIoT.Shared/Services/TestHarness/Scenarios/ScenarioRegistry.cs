#if TEST_HARNESS
using System;
using System.Collections.Generic;
using System.Linq;

namespace QiMata.MobileIoT.Services.TestHarness.Scenarios;

public sealed class ScenarioRegistry
{
    private readonly Dictionary<string, IScenario> _byName;

    public ScenarioRegistry(IEnumerable<IScenario> scenarios)
    {
        _byName = scenarios.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Names => _byName.Keys.ToArray();

    public bool TryGet(string name, out IScenario scenario)
        => _byName.TryGetValue(name, out scenario!);
}
#endif
