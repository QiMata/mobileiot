using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.ViewModels;

public partial class VisionViewModel(ImageClassificationService service, IQrScanningService qrScanner) : ObservableObject
{
    private readonly ImageClassificationService _service = service;
    private readonly IQrScanningService _qrScanner = qrScanner;

    [ObservableProperty]
    ImageSource? photo;

    [ObservableProperty]
    string result = string.Empty;

    [ObservableProperty]
    string qrResult = string.Empty;

    [RelayCommand]
    async Task CapturePhoto()
    {
        var file = await MediaPicker.CapturePhotoAsync();
        if (file is null)
            return;

        Photo = ImageSource.FromFile(file.FullPath);
        using var stream = await file.OpenReadAsync();
        Result = _service.ClassifyImage(stream);
    }

    [RelayCommand]
    async Task ScanQr()
    {
        var code = await _qrScanner.ScanAsync();
        if (code is not null)
            QrResult = code;
    }

    [RelayCommand]
    Task NavigateBack() => Shell.Current.GoToAsync("..");
}
