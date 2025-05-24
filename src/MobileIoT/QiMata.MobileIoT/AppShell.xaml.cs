using QiMata.MobileIoT.Views;

namespace QiMata.MobileIoT
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // register application routes
            Routing.RegisterRoute(nameof(BeaconPage), typeof(QiMata.MobileIoT.Views.BeaconPage));
            Routing.RegisterRoute(nameof(BleScannerPage), typeof(QiMata.MobileIoT.Views.BleScannerPage));
            Routing.RegisterRoute(nameof(BlePage), typeof(QiMata.MobileIoT.Views.BlePage));
            Routing.RegisterRoute(nameof(NfcPage), typeof(QiMata.MobileIoT.Views.NfcPage));
            Routing.RegisterRoute(nameof(NfcP2PPage), typeof(QiMata.MobileIoT.Views.NfcP2PPage));
            Routing.RegisterRoute(nameof(SerialPage), typeof(QiMata.MobileIoT.Views.SerialPage));
            Routing.RegisterRoute(nameof(UsbPage), typeof(QiMata.MobileIoT.Views.UsbPage));
            Routing.RegisterRoute(nameof(WifiDirectPage), typeof(QiMata.MobileIoT.Views.WifiDirectPage));
        }
    }
}
