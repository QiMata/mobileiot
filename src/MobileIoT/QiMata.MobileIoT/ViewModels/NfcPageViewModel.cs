using QiMata.MobileIoT.Shared.Services;
using QiMata.MobileIoT.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class NfcPageViewModel : BaseViewModel
{
    readonly INfcService _nfc;
    bool _listening;

    public NfcPageViewModel(INfcService nfc, IAppLogger logger) : base(logger)
    {
        _nfc = nfc;

        IsNfcAvailable = _nfc.IsAvailable && _nfc.IsEnabled;
        if (!_nfc.IsAvailable)
            StatusMessage = "NFC is not available on this device.";
        else if (!_nfc.IsEnabled)
            StatusMessage = "NFC is not enabled on this device.";

        if (IsNfcAvailable)
        {
            Subscribe<string>(
                h => _nfc.MessageReceived += h,
                h => _nfc.MessageReceived -= h,
                OnMessageReceived);
        }

        ListenButtonText = "Start Scan";
    }

    [ObservableProperty] string? tagContent;
    [ObservableProperty] string textToWrite = string.Empty;
    [ObservableProperty] string listenButtonText = string.Empty;
    [ObservableProperty] bool _isNfcAvailable;

    private void OnMessageReceived(object? sender, string text) => TagContent = text;

    [RelayCommand(CanExecute = nameof(IsNfcAvailable))]
    async Task ToggleListen()
    {
        if (!_listening)
        {
            await _nfc.StartListeningAsync();
            ListenButtonText = "Stop Scan";
        }
        else
        {
            await _nfc.StopListeningAsync();
            ListenButtonText = "Start Scan";
        }
        _listening = !_listening;
    }

    [RelayCommand(CanExecute = nameof(IsNfcAvailable))]
    async Task Write()
    {
        if (!string.IsNullOrWhiteSpace(TextToWrite))
            await _nfc.WriteTextAsync(TextToWrite);
    }

    [RelayCommand]
    Task NavigateBack() => Shell.Current.GoToAsync("..");
}
