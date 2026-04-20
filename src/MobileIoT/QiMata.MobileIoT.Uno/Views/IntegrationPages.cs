using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace QiMata.MobileIoT.Uno.Views;

public abstract partial class IntegrationPage : Page
{
    protected IntegrationPage(string title, string description)
    {
        var back = new Button { Content = "Back", HorizontalAlignment = HorizontalAlignment.Left };
        back.Click += async (_, _) =>
        {
            if (App.NavigationService is not null)
                await App.NavigationService.GoBackAsync();
        };

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Padding = new Thickness(24),
                Spacing = 12,
                MaxWidth = 760,
                Children =
                {
                    back,
                    new TextBlock { Text = title, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    new TextBlock { Text = description, TextWrapping = TextWrapping.WrapWholeWords },
                    new TextBlock { Text = "Harness scenarios use the shared in-app HTTP host when TestHarness mode is enabled.", TextWrapping = TextWrapping.WrapWholeWords }
                }
            }
        };
    }
}

public sealed partial class BlePage : IntegrationPage
{
    public BlePage()
        : base("BLE GATT", "Connects to a BLE sensor service, reads DHT telemetry, and toggles an LED.")
    {
    }
}

public sealed partial class BeaconPage : IntegrationPage
{
    public BeaconPage()
        : base("BLE Beacon Scan", "Scans nearby Bluetooth LE advertisements and lists beacon metadata.")
    {
    }
}

public sealed partial class NfcPage : IntegrationPage
{
    public NfcPage()
        : base("NFC Tag", "Reads and writes NDEF text records for tag-based IoT workflows.")
    {
    }
}

public sealed partial class NfcP2PPage : IntegrationPage
{
    public NfcP2PPage()
        : base("NFC Peer-to-Peer", "Shares a small NDEF message with another nearby NFC device where the platform supports it.")
    {
    }
}

public sealed partial class NfcProvisioningPage : IntegrationPage
{
    public NfcProvisioningPage()
        : base("NFC Provisioning", "Writes Wi-Fi and device provisioning payloads to NFC tags.")
    {
    }
}

public sealed partial class WifiDirectPage : IntegrationPage
{
    public WifiDirectPage()
        : base("WiFi Direct", "Discovers peers and sends direct local network messages.")
    {
    }
}

public sealed partial class P2pPage : IntegrationPage
{
    public P2pPage()
        : base("Peer Messaging", "Exercises the shared peer-to-peer service abstraction.")
    {
    }
}

public sealed partial class UsbPage : IntegrationPage
{
    public UsbPage()
        : base("USB Bulk", "Enumerates USB devices and performs a PING bulk transfer.")
    {
    }
}

public sealed partial class SerialPage : IntegrationPage
{
    public SerialPage()
        : base("USB Serial", "Sends text commands to a USB serial device and displays responses.")
    {
    }
}

public sealed partial class VisionPage : IntegrationPage
{
    public VisionPage()
        : base("Vision + Pi Camera", "Runs local image classification and talks to the Raspberry Pi camera service.")
    {
    }
}

public sealed partial class AudioPage : IntegrationPage
{
    public AudioPage()
        : base("Audio Modem", "Captures and decodes low-bandwidth audio telemetry.")
    {
    }
}

public sealed partial class ThreadPage : IntegrationPage
{
    public ThreadPage()
        : base("Thread", "Displays Thread mesh status and sends a CoAP-style ping through the bridge service.")
    {
    }
}
