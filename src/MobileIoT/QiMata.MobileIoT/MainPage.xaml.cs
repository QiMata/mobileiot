using System.Windows.Input;

namespace QiMata.MobileIoT
{
    public partial class MainPage : ContentPage
    {
        public ICommand NavigateCommand { get; }

        public MainPage()
        {
            InitializeComponent();

            NavigateCommand = new Command<string>(async (pageName) => await NavigateToPage(pageName));

            BindingContext = this;
        }

        private async System.Threading.Tasks.Task NavigateToPage(string pageName)
        {
            await Shell.Current.GoToAsync(pageName);
        }
    }
}
