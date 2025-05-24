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

        var pending = required
            .Where(p => ActivityCompat.CheckSelfPermission(activity, p) != Permission.Granted)
            .ToArray();

        if (pending.Length == 0)
            return Task.FromResult(true);

        var tcs = new TaskCompletionSource<bool>();

        void Handler(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (requestCode != 7001) return;

            MainActivity.PermissionsResultReceived -= Handler;

            var grantedPermissions = permissions
                .Zip(grantResults, (perm, result) => (perm, result))
                .Where(pr => pr.result == Permission.Granted)
                .Select(pr => pr.perm)
                .ToArray();

            var allGranted = pending.All(grantedPermissions.Contains);
            tcs.TrySetResult(allGranted);
        }

        MainActivity.PermissionsResultReceived += Handler;

        ActivityCompat.RequestPermissions(activity, pending, 7001);

        return tcs.Task;
    }
}
