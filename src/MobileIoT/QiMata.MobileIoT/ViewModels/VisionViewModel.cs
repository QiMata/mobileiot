using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Services;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.ViewModels;

public partial class VisionViewModel(
    ImageClassificationService service,
    IQrScanningService qrScanner,
    IPiCameraService piCamera) : ObservableObject
{
    private readonly ImageClassificationService _service = service;
    private readonly IQrScanningService _qrScanner = qrScanner;
    private readonly IPiCameraService _piCamera = piCamera;

    [ObservableProperty]
    ImageSource? photo;

    [ObservableProperty]
    string result = string.Empty;

    [ObservableProperty]
    string qrResult = string.Empty;

    // Pi Camera fields
    [ObservableProperty]
    string piAddress = "raspberrypi.local";

    [ObservableProperty]
    string piStatus = "Not connected";

    [ObservableProperty]
    ImageSource? piPhoto;

    [ObservableProperty]
    string piCameraResult = string.Empty;

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
    async Task CheckPiConnection()
    {
        PiStatus = "Checking...";
        bool ok = await _piCamera.CheckHealthAsync(PiAddress);
        PiStatus = ok ? "Pi Camera Online" : "Pi Camera Offline";
    }

    [RelayCommand]
    async Task CapturePiFrame()
    {
        var stream = await _piCamera.CaptureFrameAsync(PiAddress);
        if (stream is not null)
        {
            PiPhoto = ImageSource.FromStream(() => stream);
            // Also classify locally
            stream.Position = 0;
            PiCameraResult = _service.ClassifyImage(stream);
        }
        else
        {
            PiCameraResult = "Failed to capture frame";
        }
    }

    [RelayCommand]
    async Task ClassifyOnPi()
    {
        PiCameraResult = "Classifying...";
        var result = await _piCamera.ClassifyRemoteAsync(PiAddress);
        if (result.HasValue)
            PiCameraResult = $"{result.Value.label} ({result.Value.confidence:P1})";
        else
            PiCameraResult = "Classification failed";
    }

    [RelayCommand]
    Task NavigateBack() => Shell.Current.GoToAsync("..");
}
