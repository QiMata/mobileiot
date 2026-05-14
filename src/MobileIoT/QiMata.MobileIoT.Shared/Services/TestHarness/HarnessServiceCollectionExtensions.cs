#if TEST_HARNESS
using Microsoft.Extensions.DependencyInjection;
using QiMata.MobileIoT.Services.TestHarness.Scenarios;

namespace QiMata.MobileIoT.Services.TestHarness;

public static class HarnessServiceCollectionExtensions
{
    public static IServiceCollection AddMobileIoTHarness(this IServiceCollection services)
    {
        services.AddSingleton<IScenario, BleGattScenario>();
        services.AddSingleton<IScenario, BeaconScanScenario>();
        services.AddSingleton<IScenario, NfcTagScenario>();
        services.AddSingleton<IScenario, NfcP2PScenario>();
        services.AddSingleton<IScenario, NfcProvisioningScenario>();
        services.AddSingleton<IScenario, NfcHceEmulateScenario>();
        services.AddSingleton<IScenario, NfcReaderScenario>();
        services.AddSingleton<IScenario, BlePeripheralScenario>();
        services.AddSingleton<IScenario, BleP2PCentralScenario>();
        services.AddSingleton<IScenario, WifiDirectScenario>();
        services.AddSingleton<IScenario, UsbBulkScenario>();
        services.AddSingleton<IScenario, UsbSerialScenario>();
        services.AddSingleton<IScenario, VisionScenario>();
        services.AddSingleton<IScenario, AudioModemScenario>();
        services.AddSingleton<IScenario, ThreadScenario>();
        services.AddSingleton<IScenario, PiCameraScenario>();
        services.AddSingleton<ScenarioRegistry>();
        services.AddSingleton<HarnessHttpHost>();
        return services;
    }
}
#endif
