using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Thread.Services;

namespace QiMata.MobileIoT.Services;

public sealed class ShellNavigationService : INavigationService
{
    private readonly IPermissionGate _permissions;

    public ShellNavigationService(IPermissionGate permissions)
    {
        _permissions = permissions;
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");

    public async Task<bool> NavigateAsync(string route)
    {
        if (!await _permissions.EnsureForRouteAsync(route))
        {
            await Shell.Current.CurrentPage.DisplayAlert(
                "Permissions required",
                "Required permissions were not granted.",
                "OK");
            return false;
        }

        await Shell.Current.GoToAsync(route);
        return true;
    }
}
