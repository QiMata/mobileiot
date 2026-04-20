using Uno.Resizetizer;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.Interfaces;
using QiMata.MobileIoT.Services.Mock;
using QiMata.MobileIoT.Uno.Services;
#if TEST_HARNESS
using QiMata.MobileIoT.Services.TestHarness;
#endif
using QiMata.MobileIoT.ThreadDemoCore.Services;

namespace QiMata.MobileIoT.Uno;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }
    public static UnoNavigationService? NavigationService { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<HttpClient>();
                    services.AddSingleton<UnoNavigationService>();
                    services.AddSingleton<IBleDemoService>(MockServiceFactory.CreateBleDemoService());
                    services.AddSingleton<IBluetoothService>(MockServiceFactory.CreateBluetoothService());
                    services.AddSingleton<IBeaconScanner>(MockServiceFactory.CreateBeaconScanner());
                    services.AddSingleton<INfcService>(MockServiceFactory.CreateNfcService());
                    services.AddSingleton<INfcP2PService>(MockServiceFactory.CreateNfcP2PService());
                    services.AddSingleton<IP2PService>(MockServiceFactory.CreateP2PService());
                    services.AddSingleton<QiMata.MobileIoT.Usb.IUsbCommunicator>(MockServiceFactory.CreateUsbCommunicator());
                    services.AddSingleton<ISerialDeviceService>(MockServiceFactory.CreateSerialDeviceService());
                    services.AddSingleton<IQrScanningService, UnoQrScanningService>();
                    services.AddSingleton<IImageClassificationService, UnoImageClassificationService>();
                    services.AddSingleton<IPiCameraService, UnoPiCameraService>();
                    services.AddSingleton<IAudioModemService, UnoAudioModemService>();
                    services.AddSingleton<IThreadBridgeClient, ThreadBridgeClient>();
                    services.AddSingleton<IThreadDemoService, ThreadDemoService>();
#if TEST_HARNESS
                    services.AddMobileIoTHarness();
#endif
                })
            );
        MainWindow = builder.Window;

        #if DEBUG
        MainWindow.UseStudio();
#endif
                MainWindow.SetWindowIcon();

        Host = builder.Build();
        NavigationService = Host.Services.GetRequiredService<UnoNavigationService>();

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();
            NavigationService.SetFrame(rootFrame);

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
        }
        else
        {
            NavigationService.SetFrame(rootFrame);
        }

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }
#if TEST_HARNESS
        if (IsHarnessRequested(args))
        {
            Host.Services.GetRequiredService<HarnessHttpHost>().Start();
        }
#endif
        // Ensure the current window is active
        MainWindow.Activate();
    }

#if TEST_HARNESS
    private static bool IsHarnessRequested(LaunchActivatedEventArgs args)
    {
        var env = Environment.GetEnvironmentVariable("MIOT_TEST_MODE");
        return env == "1"
            || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase)
            || (args.Arguments?.Contains("--miot-test-mode", StringComparison.OrdinalIgnoreCase) ?? false);
    }
#endif

}
