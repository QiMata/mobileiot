namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>
/// Resolves and requests the permissions required for a given navigation route.
/// </summary>
public interface IPermissionGate
{
    Task<bool> EnsureForRouteAsync(string route);
}
