using Microsoft.Maui.ApplicationModel;

namespace QiMata.MobileIoT.Helpers;

public static class BlePermissions
{
    public static async Task EnsureAsync()
    {
#if ANDROID
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
#endif
    }
}
