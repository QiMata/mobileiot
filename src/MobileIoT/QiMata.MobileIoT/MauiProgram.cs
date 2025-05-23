using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using QiMata.MobileIoT.Services.I;
using QiMata.MobileIoT.Services.Mock;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Views;
using QiMata.MobileIoT.Platforms.Android;
using QiMata.MobileIoT.Platforms.iOS;
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
#if ANDROID
            builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_Android>();
#elif IOS
            builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_iOS>();
#endif
#else
            builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
            builder.Services.AddSingleton<IBleDemoService, BleDemoService>();
#if ANDROID
            builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_Android>();
#elif IOS
            builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_iOS>();
#endif
#if ANDROID
            builder.Services.AddSingleton<INfcService, AndroidNfcService>();
#elif IOS
            builder.Services.AddSingleton<INfcService, IosNfcService>();
#endif
#if ANDROID
            builder.Services.AddSingleton<INfcP2PService, NfcP2PService_Android>();
#else
            builder.Services.AddSingleton<INfcP2PService, NfcP2PService_iOS>();
#endif
#endif


            builder.Services.AddTransient<ViewModels.NfcPageViewModel>();
            builder.Services.AddTransient<NfcPage>();

            builder.Services.AddTransient<ViewModels.NfcP2PViewModel>();
            builder.Services.AddTransient<NfcP2PPage>();

            builder.Services.AddTransient<ViewModels.BleViewModel>();
            builder.Services.AddTransient<BlePage>();

            return builder.Build();
        }
    }
}
