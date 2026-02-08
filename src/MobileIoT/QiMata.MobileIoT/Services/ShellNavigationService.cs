using QiMata.MobileIoT.ThreadDemoCore.Services;

namespace QiMata.MobileIoT.Services;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
