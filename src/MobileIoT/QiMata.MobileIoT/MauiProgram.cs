using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using QiMata.MobileIoT.DependencyInjection;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Services.TestHarness;
#endif
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace QiMata.MobileIoT
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            bool isDesign = DesignMode.IsDesignModeEnabled;

            QiMata.MobileIoT.Shared.ViewModels.BaseViewModel.DispatchToMain = Helpers.DispatcherExtensions.RunOnMain;

            builder.Services
                .AddLoggingAndSharedAppServices()
                .AddPlatformServices(isDesign)
                .AddDesignOrRuntimeServices(isDesign);
#if DEBUG
            builder.Logging.AddDebug();
#endif
            builder.Services
                .AddPageAndViewModelServices()
                .AddThreadServicesAndNavigation();

#if TEST_HARNESS
            builder.Services.AddMobileIoTHarness();
#endif

            var app = builder.Build();

#if TEST_HARNESS
            app.StartMobileIoTHarnessIfEnabled();
#endif

            return app;
        }
    }
}
