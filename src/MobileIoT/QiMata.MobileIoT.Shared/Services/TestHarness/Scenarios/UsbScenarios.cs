using QiMata.MobileIoT.Shared.Services;
#if TEST_HARNESS
using QiMata.MobileIoT.Shared.Services.Interfaces;
using QiMata.MobileIoT.Shared.Usb;

namespace QiMata.MobileIoT.Shared.Services.TestHarness.Scenarios;

public sealed class UsbBulkScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "usb-bulk";

    protected override Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var usb = Get<IUsbCommunicator>();
        var devices = usb.ListDevices().ToArray();
        if (devices.Length == 0)
            return Task.FromResult(Skipped("No USB bulk devices found"));

        var target = devices[0];
        if (!usb.OpenDevice(target.Identifier))
            return Task.FromResult(Skipped($"Failed to open USB device '{target.Identifier}'"));

        var tx = Bytes(Arg(args, "message", "PING"));
        var written = usb.Write(tx);
        var rx = new byte[Math.Max(64, tx.Length)];
        var read = usb.Read(rx);
        return Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?>
        {
            ["device"] = target.Identifier,
            ["written"] = written,
            ["read"] = read,
            ["readHex"] = Convert.ToHexString(rx, 0, Math.Max(0, read))
        });
    }
}

public sealed class UsbSerialScenario(IServiceProvider services) : ScenarioBase(services)
{
    public override string Name => "usb-serial";

    protected override async Task<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken cancellationToken)
    {
        var serial = Get<ISerialDeviceService>();
        var devices = await serial.ListAsync(cancellationToken).ConfigureAwait(false);
        if (devices.Count == 0)
            return Skipped("No USB serial devices found");

        var target = devices[0];
        var opened = await serial.OpenAsync(target.VendorId, target.ProductId, 9600, cancellationToken).ConfigureAwait(false);
        if (!opened)
            return Skipped($"Failed to open serial device '{target.ProductName}'");

        var command = Arg(args, "command", "LED_ON\n");
        var written = await serial.WriteAsync(Bytes(command), cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["device"] = target.ProductName,
            ["written"] = written
        };
    }
}
#endif
