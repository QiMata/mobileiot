using Microsoft.Maui.ApplicationModel;

namespace QiMata.MobileIoT.Services;

public static class PermissionsHelper
{
    public static async Task EnsureBlePermissionsAsync()
    {
#if ANDROID
        if (DeviceInfo.Version.Major >= 12)
            await Permissions.RequestAsync<Permissions.Bluetooth>();
        else
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
#endif
    }
}
