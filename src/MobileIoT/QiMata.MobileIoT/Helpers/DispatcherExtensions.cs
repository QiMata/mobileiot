using Microsoft.Maui.ApplicationModel;

namespace QiMata.MobileIoT.Helpers;

public static class DispatcherExtensions
{
    public static void RunOnMain(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}
