namespace QiMata.MobileIoT.Views;

public partial class SerialPage : ContentPage
{
	public SerialPage(ViewModels.SerialDemoViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
