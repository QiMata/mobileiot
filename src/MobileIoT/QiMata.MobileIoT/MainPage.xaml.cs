using System.Windows.Input;

namespace QiMata.MobileIoT
{
    public partial class MainPage : ContentPage
    {
        public ICommand NavigateCommand { get; }

        readonly Dictionary<string, Func<Task<bool>>> _permissionChecks;

        public MainPage()
        {
            InitializeComponent();

            _permissionChecks = new Dictionary<string, Func<Task<bool>>>
            {
                ["BlePage"] = EnsurePermission<Permissions.LocationWhenInUse>,
                ["BleScannerPage"] = EnsurePermission<Permissions.LocationWhenInUse>,
                ["NfcPage"] = () => Task.FromResult(true),
                ["NfcP2PPage"] = () => Task.FromResult(true),
                ["UsbPage"] = () => Task.FromResult(true),
                ["SerialPage"] = () => Task.FromResult(true),
                ["WifiDirectPage"] = EnsurePermission<Permissions.NetworkState>
            };

            NavigateCommand = new Command<string>(async route => await NavigateToPage(route));

            BindingContext = this;
        }

        static async Task<bool> EnsurePermission<TPermission>() where TPermission : Permissions.BasePermission, new()
        {
            var status = await Permissions.CheckStatusAsync<TPermission>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<TPermission>();

            return status == PermissionStatus.Granted;
        }

        async Task NavigateToPage(string route)
        {
            if (_permissionChecks.TryGetValue(route, out var check) && !await check())
            {
                await DisplayAlert("Permissions required", "Required permissions were not granted.", "OK");
                return;
            }

            await Shell.Current.GoToAsync(route);
        }
    }
}
