namespace QiMata.MobileIoT.Views;

public partial class NfcProvisioningPage : ContentPage
{
    public NfcProvisioningPage(ViewModels.NfcProvisioningViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
