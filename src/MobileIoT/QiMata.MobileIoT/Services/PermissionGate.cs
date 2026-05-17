using Microsoft.Maui.ApplicationModel;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Services;

public sealed class PermissionGate : IPermissionGate
{
    private static readonly Dictionary<string, Func<Task<bool>>> Checks = new()
    {
        ["BlePage"] = EnsureBleAndLocationAsync,
        ["BleScannerPage"] = EnsureBleAndLocationAsync,
        ["WifiDirectPage"] = Ensure<Permissions.NetworkState>,
        ["VisionPage"] = Ensure<Permissions.Camera>,
    };

    public Task<bool> EnsureForRouteAsync(string route)
        => Checks.TryGetValue(route, out var check) ? check() : Task.FromResult(true);

    private static async Task<bool> Ensure<TPermission>() where TPermission : Permissions.BasePermission, new()
    {
        var status = await Permissions.CheckStatusAsync<TPermission>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<TPermission>();
        return status == PermissionStatus.Granted;
    }

    private static async Task<bool> EnsureBleAndLocationAsync()
        => await Ensure<Permissions.LocationWhenInUse>() && await Ensure<Permissions.Bluetooth>();
}
