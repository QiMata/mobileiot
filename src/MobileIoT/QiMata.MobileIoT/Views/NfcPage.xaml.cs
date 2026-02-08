using QiMata.MobileIoT.Services.Interfaces;
using QiMata.MobileIoT.ViewModels;

namespace QiMata.MobileIoT.Views;

public partial class NfcPage : ContentPage
{
    public NfcPage(NfcPageViewModel nfcPageViewModel)
    {
        InitializeComponent();
        BindingContext = nfcPageViewModel;
    }
}