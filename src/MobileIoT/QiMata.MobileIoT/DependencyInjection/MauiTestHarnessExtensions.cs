#if TEST_HARNESS
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using QiMata.MobileIoT.Shared.Services.TestHarness;

namespace QiMata.MobileIoT.DependencyInjection;

public static class MauiTestHarnessExtensions
{
    public static void StartMobileIoTHarnessIfEnabled(this MauiApp app)
    {
        if (!IsTestHarnessEnabled())
        {
            return;
        }

        var host = app.Services.GetRequiredService<HarnessHttpHost>();
        host.Start();
    }

    private static bool IsTestHarnessEnabled()
    {
#if ANDROID
        return true;
#else
        var env = Environment.GetEnvironmentVariable("MIOT_TEST_MODE");
        return !string.IsNullOrEmpty(env) && env != "0";
#endif
    }
}
#endif
