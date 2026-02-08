using QiMata.MobileIoT.ThreadDemoCore.ViewModels;

namespace QiMata.MobileIoT.Views;

public partial class ThreadPage : ContentPage
{
    public ThreadPage(ThreadViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
