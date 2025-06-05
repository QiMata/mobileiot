using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Services;

namespace QiMata.MobileIoT.ViewModels;

public partial class VisionViewModel(ImageClassificationService service) : ObservableObject
{
    private readonly ImageClassificationService _service = service;

    [ObservableProperty]
    ImageSource? photo;

    [ObservableProperty]
    string result = string.Empty;

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
    Task NavigateBack() => Shell.Current.GoToAsync("..");
}
