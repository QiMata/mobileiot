namespace QiMata.MobileIoT.Views;

public partial class WifiDirectPage : ContentPage
{
	public WifiDirectPage(ViewModels.WifiDirectViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
