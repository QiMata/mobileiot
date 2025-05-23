using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QiMata.MobileIoT.Services.I;

namespace QiMata.MobileIoT.ViewModels
{
    public partial class NfcPageViewModel : ObservableObject
    {
        readonly INfcService _nfc;
        bool _listening;

        public NfcPageViewModel(INfcService nfc)
        {
            _nfc = nfc;
            if (!_nfc.IsAvailable)
                throw new NotSupportedException("NFC is not available on this device.");
            if (!_nfc.IsEnabled)
                throw new NotSupportedException("NFC is not enabled on this device.");
            _nfc.MessageReceived += (_, text) => TagContent = text;
            ListenButtonText = "Start Scan";
        }

        [ObservableProperty] string? tagContent;
        [ObservableProperty] string textToWrite = string.Empty;
        [ObservableProperty] string listenButtonText;

        [RelayCommand]
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

        [RelayCommand]
        async Task Write()
        {
            if (!string.IsNullOrWhiteSpace(TextToWrite))
                await _nfc.WriteTextAsync(TextToWrite);
        }

        [RelayCommand]
        Task NavigateBack() => Shell.Current.GoToAsync("..");
    }
}
