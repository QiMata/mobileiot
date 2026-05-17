using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Services.Mock;
using QiMata.MobileIoT.Shared.Thread.Services;
using QiMata.MobileIoT.Shared.Usb;
using QiMata.MobileIoT.Uno.Services;

#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Services.TestHarness;
#endif

namespace QiMata.MobileIoT.Uno.DependencyInjection;

public static class UnoServiceCollectionExtensions
{
    public static IServiceCollection AddUnoServices(this IServiceCollection services)
    {
        services.AddUnoNavigationServices();
        services.AddUnoSharedMockServices();
        services.AddUnoStubs();
        services.AddUnoThreadServices();
#if TEST_HARNESS
        services.AddUnoTestHarnessServices();
#endif
        return services;
    }

    public static IServiceCollection AddUnoNavigationServices(this IServiceCollection services)
    {
        services.AddSingleton<UnoNavigationService>();
        return services;
    }

    public static IServiceCollection AddUnoSharedMockServices(this IServiceCollection services)
    {
        services.AddSingleton<IBleDemoService>(MockServiceFactory.CreateBleDemoService());
        services.AddSingleton<IBluetoothService>(MockServiceFactory.CreateBluetoothService());
        services.AddSingleton<IBeaconScanner>(MockServiceFactory.CreateBeaconScanner());
        services.AddSingleton<INfcService>(MockServiceFactory.CreateNfcService());
        services.AddSingleton<INfcP2PService>(MockServiceFactory.CreateNfcP2PService());
        services.AddSingleton<IP2PService>(MockServiceFactory.CreateP2PService());
        services.AddSingleton<IUsbCommunicator>(MockServiceFactory.CreateUsbCommunicator());
        services.AddSingleton<ISerialDeviceService>(MockServiceFactory.CreateSerialDeviceService());
        return services;
    }

    public static IServiceCollection AddUnoStubs(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IQrScanningService, UnoQrScanningService>();
        services.AddSingleton<IImageClassificationService, UnoImageClassificationService>();
        services.AddSingleton<IPiCameraService, UnoPiCameraService>();
        services.AddSingleton<IAudioModemService, UnoAudioModemService>();
        return services;
    }

    public static IServiceCollection AddUnoThreadServices(this IServiceCollection services)
    {
        services.AddSingleton<IThreadBridgeClient, ThreadBridgeClient>();
        services.AddSingleton<IThreadDemoService, ThreadDemoService>();
        return services;
    }

#if TEST_HARNESS
    public static IServiceCollection AddUnoTestHarnessServices(this IServiceCollection services)
    {
        services.AddMobileIoTHarness();
        return services;
    }
#endif
}
