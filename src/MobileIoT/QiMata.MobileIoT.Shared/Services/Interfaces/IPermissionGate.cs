namespace QiMata.MobileIoT.Shared.Services.Interfaces;

/// <summary>
/// Resolves and requests the permissions required for a given navigation route.
/// </summary>
public interface IPermissionGate
{
    /// <summary>Checks and, if necessary, requests the permissions required to navigate to the given route, returning true if all are granted.</summary>
    Task<bool> EnsureForRouteAsync(string route);
}
