using Android.App;
using Android.Content.PM;
using Android;
using AndroidX.Core.App;
using System.Linq;
using System.Threading.Tasks;

namespace QiMata.MobileIoT.Platforms.Android.Helpers;

public static partial class PermissionHelper
{
    public static Task<bool> EnsureWifiDirectPermissionsAsync(Activity activity)
    {
        string[] required =
        {
            Manifest.Permission.AccessFineLocation,          // API ≤ 32
            "android.permission.NEARBY_WIFI_DEVICES"         // API 33+
        };

        var pending = required.Where(p => ActivityCompat.CheckSelfPermission(activity, p) != Permission.Granted)
                              .ToArray();

        if (pending.Length == 0) return Task.FromResult(true);

        var tcs = new TaskCompletionSource<bool>();
        ActivityCompat.RequestPermissions(activity, pending, 7001);

        void Handler(object? s, EventArgs e)
        {
            activity.RequestPermissionsResult -= Handler;
            var allGranted = pending.All(p => ActivityCompat.CheckSelfPermission(activity, p) == Permission.Granted);
            tcs.TrySetResult(allGranted);
        }

        activity.RequestPermissionsResult += Handler;
        return tcs.Task;
    }
}
