using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.Views;

public partial class NfcPage : ContentPage
{
    readonly INfcService _nfc;

    public NfcPage(INfcService nfc)
    {
        InitializeComponent();
        _nfc = nfc;
    }

    async void OnStartClicked(object sender, EventArgs e)
    {
        if (!_nfc.IsAvailable)
        {
            await DisplayAlert("NFC", "Not available!", "OK");
            return;
        }
        if (!_nfc.IsEnabled)
        {
            await DisplayAlert("NFC", "Please enable NFC", "OK");
            return;
        }
        await _nfc.StartListeningAsync();
    }

    async void OnStopClicked(object sender, EventArgs e)
    {
        await _nfc.StopListeningAsync();
    }

    async void OnWriteClicked(object sender, EventArgs e)
    {
        var txt = EntryText.Text ?? string.Empty;
        await _nfc.WriteTextAsync(txt);
    }
}