namespace QiMata.MobileIoT.ThreadDemoCore.Services;

public interface INavigationService
{
    Task GoBackAsync();
    Task<bool> NavigateAsync(string route);
}
