using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using QiMata.MobileIoT.Services.I;
using QiMata.MobileIoT.Services.Mock;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Views;
using Plugin.NFC;

namespace QiMata.MobileIoT
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddSingleton<IBleDemoService>(MockServiceFactory.CreateBleDemoService());
            builder.Services.AddSingleton<IBluetoothService>(MockServiceFactory.CreateBluetoothService());
            builder.Services.AddSingleton<INfcService>(MockServiceFactory.CreateNfcService());
            builder.Services.AddSingleton<INfcP2PService>(MockServiceFactory.CreateNfcP2PService());
            builder.Services.AddSingleton<IBeaconScanner>(MockServiceFactory.CreateBeaconScanner());
#else
            builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
            builder.Services.AddSingleton<IBleDemoService, BleDemoService>();
#if ANDROID
            using QiMata.MobileIoT.Platforms.Android;
            builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_Android>();
            builder.Services.AddSingleton<INfcService, AndroidNfcService>();
            builder.Services.AddSingleton<INfcP2PService, NfcP2PService_Android>();
#elif IOS
            using QiMata.MobileIoT.Platforms.iOS;
            builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_iOS>();
            builder.Services.AddSingleton<INfcService, IosNfcService>();
            builder.Services.AddSingleton<INfcP2PService, NfcP2PService_iOS>();
#endif
#endif


            builder.Services.AddTransient<ViewModels.NfcPageViewModel>();
            builder.Services.AddTransient<NfcPage>();

            builder.Services.AddTransient<ViewModels.NfcP2PViewModel>();
            builder.Services.AddTransient<NfcP2PPage>();

            builder.Services.AddTransient<ViewModels.BleViewModel>();
            builder.Services.AddTransient<BlePage>();

            builder.Services.AddTransient<ViewModels.BeaconScanViewModel>();
            builder.Services.AddTransient<BleScannerPage>();

            builder.Services.AddTransient<ViewModels.UsbDemoViewModel>();
            builder.Services.AddTransient<UsbDemoPage>();

#if ANDROID
            builder.Services.AddSingleton<IP2PService, Platforms.Android.WifiDirectService>();
#elif IOS
            builder.Services.AddSingleton<IP2PService, Platforms.iOS.MultipeerService>();
#endif

#if ANDROID
            builder.Services.AddSingleton<IUsbSerialPort, Platforms.Android.UsbSerialPortAndroid>();
#elif IOS
            builder.Services.AddSingleton<IUsbSerialPort, Platforms.iOS.UsbSerialPortIos>();
#endif

            return builder.Build();
        }
    }
}
