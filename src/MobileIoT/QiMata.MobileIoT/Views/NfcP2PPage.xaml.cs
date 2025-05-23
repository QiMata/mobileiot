using QiMata.MobileIoT.ViewModels;

namespace QiMata.MobileIoT.Views;

public partial class NfcP2PPage : ContentPage
{
    public NfcP2PPage(NfcP2PViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
