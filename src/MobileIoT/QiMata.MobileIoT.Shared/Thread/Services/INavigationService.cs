namespace QiMata.MobileIoT.Shared.Thread.Services;

public interface INavigationService
{
    Task GoBackAsync();
    Task<bool> NavigateAsync(string route);
}
