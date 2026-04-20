namespace QiMata.MobileIoT.Uno.Services;

public sealed class UnoNavigationService
{
    private Frame? _frame;

    private static readonly Dictionary<string, Type> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ble"] = typeof(Views.BlePage),
        ["beacon"] = typeof(Views.BeaconPage),
        ["nfc"] = typeof(Views.NfcPage),
        ["nfc-p2p"] = typeof(Views.NfcP2PPage),
        ["nfc-provisioning"] = typeof(Views.NfcProvisioningPage),
        ["wifi-direct"] = typeof(Views.WifiDirectPage),
        ["p2p"] = typeof(Views.P2pPage),
        ["usb"] = typeof(Views.UsbPage),
        ["serial"] = typeof(Views.SerialPage),
        ["vision"] = typeof(Views.VisionPage),
        ["audio"] = typeof(Views.AudioPage),
        ["thread"] = typeof(Views.ThreadPage),
    };

    public void SetFrame(Frame frame) => _frame = frame;

    public Task NavigateAsync(string route)
    {
        if (_frame is null)
            return Task.CompletedTask;
        if (!Routes.TryGetValue(route, out var pageType))
            throw new InvalidOperationException($"Unknown Uno route '{route}'");
        _frame.Navigate(pageType);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        if (_frame?.CanGoBack == true)
            _frame.GoBack();
        return Task.CompletedTask;
    }
}
