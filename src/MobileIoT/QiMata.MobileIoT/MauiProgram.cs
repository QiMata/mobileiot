using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.NFC;
#if ANDROID
using QiMata.MobileIoT.Platforms.Android;
#elif IOS
using QiMata.MobileIoT.Platforms.iOS;
#endif
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.I;
using QiMata.MobileIoT.Services.Mock;
using QiMata.MobileIoT.Usb;
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

            bool isDesign = true;

#if ANDROID
            if (!isDesign)
            {
                builder.Services.AddSingleton<IBeaconScanner, BeaconScanner_Android>();
                builder.Services.AddSingleton<INfcService, AndroidNfcService>();
                builder.Services.AddSingleton<INfcP2PService, NfcP2PService_Android>();
                builder.Services.AddSingleton<IP2PService, Platforms.Android.WifiDirectService>();
                builder.Services.AddSingleton<IUsbCommunicator, Platforms.Android.UsbCommunicatorAndroid>();
                builder.Services.AddSingleton<ISerialDeviceService, Platforms.Android.UsbSerialDeviceService>();
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

            return builder.Build();
        }
    }
}
