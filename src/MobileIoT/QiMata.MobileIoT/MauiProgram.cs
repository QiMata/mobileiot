using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using QiMata.MobileIoT.Services.I;
using QiMata.MobileIoT.Services.Mock;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Views;

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
#else
            builder.Services.AddSingleton<IBleDemoService, BleDemoService>();
#endif
            builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
            builder.Services.AddTransient<ViewModels.BleViewModel>();
            builder.Services.AddTransient<BlePage>();

            return builder.Build();
        }
    }
}
