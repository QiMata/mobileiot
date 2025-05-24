namespace QiMata.MobileIoT.Views;

public partial class BleScannerPage : ContentPage
{
    public BleScannerPage(ViewModels.BeaconScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
