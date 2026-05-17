using QiMata.MobileIoT.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Plugin.NFC;
#if ANDROID
using QiMata.MobileIoT.Platforms.Android;
#elif IOS
using QiMata.MobileIoT.Platforms.iOS;
#endif
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Services.Mock;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Services.TestHarness;
#endif
using QiMata.MobileIoT.Shared.Thread.Services;
using QiMata.MobileIoT.ViewModels;
using QiMata.MobileIoT.Shared.Usb;
using QiMata.MobileIoT.Views;
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

            builder.Services.AddSingleton<AppLogger>();
            builder.Services.AddSingleton<IAppLogger>(sp => sp.GetRequiredService<AppLogger>());
            builder.Services.AddSingleton<IObservableLog>(sp => sp.GetRequiredService<AppLogger>());

#if ANDROID
            if (!isDesign)
            {
                builder.Services.AddSingleton<Platforms.Android.Services.UsbPermissionManager>();
                builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_Android>();
                builder.Services.AddSingleton<INfcService, AndroidNfcService>();
                builder.Services.AddSingleton<INfcP2PService, NfcP2PService_Android>();
                builder.Services.AddSingleton<IP2PService, Platforms.Android.WifiDirectService>();
                builder.Services.AddSingleton<IUsbCommunicator, Platforms.Android.UsbCommunicatorAndroid>();
                builder.Services.AddSingleton<ISerialDeviceService, Platforms.Android.UsbSerialDeviceService>();
#if TEST_HARNESS
                builder.Services.AddSingleton<IHceService, Platforms.Android.HceService_Android>();
                builder.Services.AddSingleton<INfcReaderService, Platforms.Android.NfcReaderService_Android>();
                builder.Services.AddSingleton<IBleAdvertiseService, Platforms.Android.BleAdvertiseService_Android>();
                builder.Services.AddSingleton<IBleP2PCentralService, Platforms.Android.BleP2PCentralService_Android>();
#endif
            }


#elif IOS
            if (!isDesign)
            {
                builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_iOS>();
                builder.Services.AddSingleton<INfcService, IosNfcService>();
                builder.Services.AddSingleton<INfcP2PService, NfcP2PService_iOS>();
                builder.Services.AddSingleton<IP2PService, Platforms.iOS.MultipeerService>();
                builder.Services.AddSingleton<IUsbCommunicator,Platforms.iOS.UsbCommunicatoriOS>();
                builder.Services.AddSingleton<ISerialDeviceService, Platforms.iOS.ExternalAccessorySerialDeviceService>();
#if TEST_HARNESS
                builder.Services.AddSingleton<IBleAdvertiseService, Platforms.iOS.BleAdvertiseService_iOS>();
                builder.Services.AddSingleton<IBleP2PCentralService, Platforms.iOS.BleP2PCentralService_iOS>();
#endif
            }
#endif
#if DEBUG
            builder.Logging.AddDebug();
#endif
            if (isDesign)
            {
                builder.Services.AddSingleton<IBleDemoService>(MockServiceFactory.CreateBleDemoService());
                builder.Services.AddSingleton<IBluetoothService>(MockServiceFactory.CreateBluetoothService());
                builder.Services.AddSingleton<IBeaconScanner>(MockServiceFactory.CreateBeaconScanner());
                builder.Services.AddSingleton<INfcService>(MockServiceFactory.CreateNfcService());
                builder.Services.AddSingleton<INfcP2PService>(MockServiceFactory.CreateNfcP2PService());
                builder.Services.AddSingleton<IP2PService>(MockServiceFactory.CreateP2PService());
                builder.Services.AddSingleton<IUsbCommunicator>(MockServiceFactory.CreateUsbCommunicator());
                builder.Services.AddSingleton<ISerialDeviceService>(MockServiceFactory.CreateSerialDeviceService());
            }
            else
            {
                builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
                builder.Services.AddSingleton<IBleDemoService, BleDemoService>();
            }

            builder.Services.AddSingleton<IQrScanningService, QrScanningService>();
            builder.Services.AddSingleton<ImageClassificationService>();
            builder.Services.AddSingleton<IImageClassificationService>(sp => sp.GetRequiredService<ImageClassificationService>());
            builder.Services.AddSingleton<IPiCameraService, PiCameraService>();
            builder.Services.AddSingleton<IAudioDecoder, RootMeanSquareAudioDecoder>();
            builder.Services.AddSingleton<IAudioModemService, AudioModemService>();


            builder.Services.AddTransient<ViewModels.NfcPageViewModel>();
            builder.Services.AddTransient<NfcPage>();

            builder.Services.AddTransient<ViewModels.NfcP2PViewModel>();
            builder.Services.AddTransient<NfcP2PPage>();

            builder.Services.AddTransient<ViewModels.BleViewModel>();
            builder.Services.AddTransient<BlePage>();

            builder.Services.AddTransient<ViewModels.BeaconScanViewModel>();
            builder.Services.AddTransient<BleScannerPage>();

            builder.Services.AddTransient<ViewModels.UsbViewModel>();
            builder.Services.AddTransient<UsbPage>();

            builder.Services.AddTransient<ViewModels.VisionViewModel>();
            builder.Services.AddTransient<VisionPage>();

            builder.Services.AddTransient<ViewModels.SerialDemoViewModel>();
            builder.Services.AddTransient<SerialPage>();

            builder.Services.AddTransient<ViewModels.WifiDirectViewModel>();
            builder.Services.AddTransient<WifiDirectPage>();

            builder.Services.AddTransient<ViewModels.NfcProvisioningViewModel>();
            builder.Services.AddTransient<NfcProvisioningPage>();

            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<IThreadBridgeClient, ThreadBridgeClient>();
            builder.Services.AddSingleton<IThreadDemoService, ThreadDemoService>();
            builder.Services.AddSingleton<IPermissionGate, PermissionGate>();
            builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
            builder.Services.AddTransient<MainPageViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ThreadViewModel>();
            builder.Services.AddTransient<ThreadPage>();
            builder.Services.AddTransient<ViewModels.AudioDemoViewModel>();
            builder.Services.AddTransient<AudioPage>();

#if TEST_HARNESS
            builder.Services.AddMobileIoTHarness();
#endif

            var app = builder.Build();

#if TEST_HARNESS
            if (IsTestHarnessEnabled())
            {
                var host = app.Services.GetRequiredService<HarnessHttpHost>();
                host.Start();
            }
#endif

            return app;
        }

#if TEST_HARNESS
        private static bool IsTestHarnessEnabled()
        {
#if ANDROID
            return true;
#else
            var env = Environment.GetEnvironmentVariable("MIOT_TEST_MODE");
            return !string.IsNullOrEmpty(env) && env != "0";
#endif
        }
#endif
    }
}
