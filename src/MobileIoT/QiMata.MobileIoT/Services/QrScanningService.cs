using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace QiMata.MobileIoT.Services;

public class QrScanningService : IQrScanningService
{
    public async Task<string?> ScanAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                return null;
        }

        var cameraView = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.QrCode,
                AutoRotate = true,
                Multiple = false
            },
            HorizontalOptions = LayoutOptions.FillAndExpand,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        var page = new ContentPage { Content = cameraView };
        var tcs = new TaskCompletionSource<string?>();

        cameraView.BarcodesDetected += (s, e) =>
        {
            var result = e.Results.FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(result))
            {
                cameraView.IsDetecting = false;
                tcs.TrySetResult(result);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current!.MainPage!.Navigation.PopModalAsync();
                });
            }
        };

        await Application.Current!.MainPage!.Navigation.PushModalAsync(page);
        return await tcs.Task;
    }
}
