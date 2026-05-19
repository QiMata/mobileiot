using Microsoft.Extensions.DependencyInjection;
using QiMata.MobileIoT;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Services.Mock;
using QiMata.MobileIoT.Shared.Thread.Services;
using QiMata.MobileIoT.Shared.Usb;
using QiMata.MobileIoT.ViewModels;
using QiMata.MobileIoT.Views;

#if ANDROID
using QiMata.MobileIoT.Platforms.Android;
#elif IOS
using QiMata.MobileIoT.Platforms.iOS;
#endif

namespace QiMata.MobileIoT.DependencyInjection;

public static class MauiServiceCollectionExtensions
{
    public static IServiceCollection AddLoggingAndSharedAppServices(this IServiceCollection services)
    {
        services.AddSingleton<AppLogger>();
        services.AddSingleton<IAppLogger>(sp => sp.GetRequiredService<AppLogger>());
        services.AddSingleton<IObservableLog>(sp => sp.GetRequiredService<AppLogger>());

        services.AddSingleton<IQrScanningService, QrScanningService>();
        services.AddSingleton<ImageClassificationService>();
        services.AddSingleton<IImageClassificationService>(sp => sp.GetRequiredService<ImageClassificationService>());
        services.AddSingleton<IPiCameraService, PiCameraService>();
        services.AddSingleton<IAudioDecoder, RootMeanSquareAudioDecoder>();
        services.AddSingleton<IAudioModemService, AudioModemService>();

        return services;
    }

    public static IServiceCollection AddPlatformServices(this IServiceCollection services, bool isDesign)
    {
        if (isDesign)
        {
            return services;
        }

#if ANDROID
        services.AddSingleton<Platforms.Android.Services.UsbPermissionManager>();
        services.AddSingleton<IBeaconScanner, BeaconScanner_Android>();
        services.AddSingleton<INfcService, AndroidNfcService>();
        services.AddSingleton<INfcP2PService, NfcP2PService_Android>();
        services.AddSingleton<IP2PService, Platforms.Android.WifiDirectService>();
        services.AddSingleton<IUsbCommunicator, Platforms.Android.UsbCommunicatorAndroid>();
        services.AddSingleton<ISerialDeviceService, Platforms.Android.UsbSerialDeviceService>();
#if TEST_HARNESS
        services.AddSingleton<IHceService, Platforms.Android.HceService_Android>();
        services.AddSingleton<INfcReaderService, Platforms.Android.NfcReaderService_Android>();
        services.AddSingleton<IBleAdvertiseService, Platforms.Android.BleAdvertiseService_Android>();
        services.AddSingleton<IBleP2PCentralService, Platforms.Android.BleP2PCentralService_Android>();
#endif
#elif IOS
        services.AddSingleton<IBeaconScanner, BeaconScanner_iOS>();
        services.AddSingleton<INfcService, IosNfcService>();
        services.AddSingleton<INfcP2PService, NfcP2PService_iOS>();
        services.AddSingleton<IP2PService, Platforms.iOS.MultipeerService>();
        services.AddSingleton<IUsbCommunicator, Platforms.iOS.UsbCommunicatoriOS>();
        services.AddSingleton<ISerialDeviceService, Platforms.iOS.ExternalAccessorySerialDeviceService>();
#if TEST_HARNESS
        services.AddSingleton<IBleAdvertiseService, Platforms.iOS.BleAdvertiseService_iOS>();
        services.AddSingleton<IBleP2PCentralService, Platforms.iOS.BleP2PCentralService_iOS>();
#endif
#endif

        return services;
    }

    public static IServiceCollection AddDesignOrRuntimeServices(this IServiceCollection services, bool isDesign)
    {
        App.IsMockMode = isDesign;

        if (isDesign)
        {
            services.AddSingleton<IBleDemoService>(MockServiceFactory.CreateBleDemoService());
            services.AddSingleton<IBluetoothService>(MockServiceFactory.CreateBluetoothService());
            services.AddSingleton<IBeaconScanner>(MockServiceFactory.CreateBeaconScanner());
            services.AddSingleton<INfcService>(MockServiceFactory.CreateNfcService());
            services.AddSingleton<INfcP2PService>(MockServiceFactory.CreateNfcP2PService());
            services.AddSingleton<IP2PService>(MockServiceFactory.CreateP2PService());
            services.AddSingleton<IUsbCommunicator>(MockServiceFactory.CreateUsbCommunicator());
            services.AddSingleton<ISerialDeviceService>(MockServiceFactory.CreateSerialDeviceService());
        }
        else
        {
            services.AddSingleton<IBluetoothService, BluetoothService>();
            services.AddSingleton<IBleDemoService, BleDemoService>();
        }

        return services;
    }

    public static IServiceCollection AddPageAndViewModelServices(this IServiceCollection services)
    {
        services.AddTransient<NfcPageViewModel>();
        services.AddTransient<NfcPage>();

        services.AddTransient<NfcP2PViewModel>();
        services.AddTransient<NfcP2PPage>();

        services.AddTransient<BleViewModel>();
        services.AddTransient<BlePage>();

        services.AddTransient<BeaconScanViewModel>();
        services.AddTransient<BleScannerPage>();

        services.AddTransient<UsbViewModel>();
        services.AddTransient<UsbPage>();

        services.AddTransient<VisionViewModel>();
        services.AddTransient<VisionPage>();

        services.AddTransient<SerialDemoViewModel>();
        services.AddTransient<SerialPage>();

        services.AddTransient<WifiDirectViewModel>();
        services.AddTransient<WifiDirectPage>();

        services.AddTransient<NfcProvisioningViewModel>();
        services.AddTransient<NfcProvisioningPage>();

        return services;
    }

    public static IServiceCollection AddThreadServicesAndNavigation(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IThreadBridgeClient, ThreadBridgeClient>();
        services.AddSingleton<IThreadDemoService, ThreadDemoService>();
        services.AddSingleton<IPermissionGate, PermissionGate>();
        services.AddSingleton<INavigationService, ShellNavigationService>();
        services.AddTransient<MainPageViewModel>();
        services.AddTransient<MainPage>();
        services.AddTransient<ThreadViewModel>();
        services.AddTransient<ThreadPage>();
        services.AddTransient<AudioDemoViewModel>();
        services.AddTransient<AudioPage>();

        return services;
    }
}
