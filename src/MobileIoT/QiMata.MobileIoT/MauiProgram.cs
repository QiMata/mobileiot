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
#else
            builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
            builder.Services.AddSingleton<IBleDemoService, BleDemoService>();
#if ANDROID
            builder.Services.AddSingleton<INfcService, AndroidNfcService>();
#elif IOS
            builder.Services.AddSingleton<INfcService, IosNfcService>();
#endif
#endif



            builder.Services.AddTransient<ViewModels.BleViewModel>();
            builder.Services.AddTransient<BlePage>();
            builder.Services.AddTransient<Views.NfcPage>();

            return builder.Build();
        }
    }
}
