using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Plugin.NFC;

namespace QiMata.MobileIoT;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
          ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CrossNFC.Init(this);
    }

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);
        CrossNFC.OnNewIntent(intent);
    }
}
