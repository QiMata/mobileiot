// Program.cs new snippet
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using Linux.Bluetooth;

[DBusInterface("org.bluez.LEAdvertisingManager1")]
interface ILEAdvertisingManager1 : IDBusObject
{
    Task<ObjectPath> RegisterAdvertisementAsync(ObjectPath advertisement,
                                                IDictionary<string, object> options);
    Task UnregisterAdvertisementAsync(ObjectPath advertisement);
}

[DBusInterface("org.bluez.LEAdvertisement1")]
interface ILEAdvertisement1 : IDBusObject
{
    Task ReleaseAsync();
}

internal readonly record struct BeaconConfig(Guid Uuid, ushort Major, ushort Minor, sbyte TxPower);

sealed class BeaconAdvert : ILEAdvertisement1, IProperties
{
    public ObjectPath ObjectPath { get; } = new("/org/ble/advertisement0");
    private readonly BeaconConfig _cfg;

    public BeaconAdvert(BeaconConfig cfg) => _cfg = cfg;

    public Task ReleaseAsync() => Task.CompletedTask;

    public Task<IDictionary<string, object>> GetAllAsync(string iface)
    {
        if (iface != "org.bluez.LEAdvertisement1")
            return Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());

        var props = new Dictionary<string, object>
        {
            { "Type", "broadcast" },
            { "ServiceUUIDs", Array.Empty<string>() },
            { "ManufacturerData", new Dictionary<ushort, object>
                {
                    { 0x004C, BuildIBeaconFrame(_cfg) }
                }
            },
            { "Includes", new string[] { "tx-power" } }
        };
        return Task.FromResult<IDictionary<string, object>>(props);
    }

    public Task<object?> GetAsync(string iface, string prop)
        => Task.FromException<object?>(new NotSupportedException());
    public Task SetAsync(string iface, string prop, object val)
        => Task.FromException(new NotSupportedException());
    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
        => Task.FromResult<IDisposable>(new Dummy());

    private sealed class Dummy : IDisposable { public void Dispose() { } }

    private static byte[] BuildIBeaconFrame(BeaconConfig c)
    {
        Span<byte> buf = stackalloc byte[23];
        buf[0] = 0x02; buf[1] = 0x15;
        Span<byte> le = stackalloc byte[16];
        c.Uuid.TryWriteBytes(le);
        buf[2] = le[3]; buf[3] = le[2]; buf[4] = le[1]; buf[5] = le[0];
        buf[6] = le[5]; buf[7] = le[4]; buf[8] = le[7]; buf[9] = le[6];
        le[8..].CopyTo(buf[10..]);
        buf[18] = (byte)(c.Major >> 8);
        buf[19] = (byte)(c.Major);
        buf[20] = (byte)(c.Minor >> 8);
        buf[21] = (byte)(c.Minor);
        buf[22] = unchecked((byte)c.TxPower);
        return buf.ToArray();
    }
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var cfg = new BeaconConfig(
            args.Length > 0 && Guid.TryParse(args[0], out var id) ? id
                : Guid.Parse("fda50693-a4e2-4fb1-afcf-c6eb07647825"),
            args.Length > 1 && ushort.TryParse(args[1], out var maj) ? maj : (ushort)100,
            args.Length > 2 && ushort.TryParse(args[2], out var min) ? min : (ushort)1,
            args.Length > 3 && sbyte.TryParse(args[3], out var tx)  ? tx  : (sbyte)-59);

        await BlueZManager.InitializeAsync();
        var adapter = (await BlueZManager.GetAdaptersAsync()).FirstOrDefault()
                     ?? throw new InvalidOperationException("No BLE adapter found.");

        if (!await adapter.GetPoweredAsync())
            await adapter.SetPoweredAsync(true);

        var conn     = Connection.System;
        var advMgr   = conn.CreateProxy<ILEAdvertisingManager1>("org.bluez", adapter.ObjectPath);

        var advert   = new BeaconAdvert(cfg);
        await conn.RegisterObject(advert);

        var advPath  = await advMgr.RegisterAdvertisementAsync(advert.ObjectPath,
                                                               new Dictionary<string, object>());
        Console.WriteLine("▲  iBeacon advertising — Ctrl-C to stop");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try { await Task.Delay(Timeout.Infinite, cts.Token); }
        catch (OperationCanceledException) { }

        await advMgr.UnregisterAdvertisementAsync(advert.ObjectPath);
        await conn.UnregisterObjectAsync(advert.ObjectPath);

        Console.WriteLine("■  Advertising stopped – goodbye");
    }
}
