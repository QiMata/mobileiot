// Program.cs  – iBeacon advertiser for BlueZ
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using Linux.Bluetooth;           // BlueZManager + typed Adapter proxy

/* ------------------------------------------------------------------------- */
/* 1.  D-Bus interface definitions                                           */
/* ------------------------------------------------------------------------- */
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

[DBusInterface("org.freedesktop.DBus.Properties")]
interface IProperties : IDBusObject
{
    Task<object?> GetAsync(string iface, string prop);
    Task<IDictionary<string, object>> GetAllAsync(string iface);
    Task SetAsync(string iface, string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

/* ------------------------------------------------------------------------- */
/* 2.  Simple record with beacon settings                                    */
/* ------------------------------------------------------------------------- */
internal readonly record struct BeaconCfg(Guid Uuid, ushort Major, ushort Minor, sbyte TxPower);

/* ------------------------------------------------------------------------- */
/* 3.  Our advertisement object                                              */
/* ------------------------------------------------------------------------- */
sealed class BeaconAdvert : ILEAdvertisement1, IProperties
{
    public ObjectPath ObjectPath { get; } = new("/org/ble/advert0");
    private readonly BeaconCfg _cfg;
    public BeaconAdvert(BeaconCfg cfg) => _cfg = cfg;

    /* --- ILEAdvertisement1 ------------------------------------------------ */
    public Task ReleaseAsync() => Task.CompletedTask;

    /* --- org.freedesktop.DBus.Properties ---------------------------------- */
    public Task<IDictionary<string, object>> GetAllAsync(string iface)
    {
        if (iface != "org.bluez.LEAdvertisement1")
            return Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());

        return Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
        {
            { "Type", "broadcast" },                       // non-connectable
            { "ManufacturerData", new Dictionary<ushort, object>
                {
                    { 0x004C, BuildFrame(_cfg) }           // Apple company-ID
                }
            },
            { "Includes", new[] { "tx-power" } }           // flags section
        });
    }
    public Task<object?> GetAsync(string _, string __) => Task.FromException<object?>(new NotSupportedException());
    public Task SetAsync(string _, string __, object ___) => Task.FromException(new NotSupportedException());
    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> _) => Task.FromResult<IDisposable>(new Dummy());
    private sealed class Dummy : IDisposable { public void Dispose() { } }

    /* --- helper: iBeacon payload ----------------------------------------- */
    private static byte[] BuildFrame(BeaconCfg c)
    {
        Span<byte> buf = stackalloc byte[23];
        buf[0] = 0x02; buf[1] = 0x15;                       // prefix

        Span<byte> le = stackalloc byte[16];
        c.Uuid.TryWriteBytes(le);
        buf[2] = le[3]; buf[3] = le[2]; buf[4] = le[1]; buf[5] = le[0];
        buf[6] = le[5]; buf[7] = le[4]; buf[8] = le[7]; buf[9] = le[6];
        le[8..].CopyTo(buf[10..]);

        buf[18] = (byte)(c.Major >> 8); buf[19] = (byte)c.Major;
        buf[20] = (byte)(c.Minor >> 8); buf[21] = (byte)c.Minor;
        buf[22] = unchecked((byte)c.TxPower);
        return buf.ToArray();
    }
}

/* ------------------------------------------------------------------------- */
/* 4.  Main program                                                          */
/* ------------------------------------------------------------------------- */
internal static class Program
{
    private static async Task Main(string[] args)
    {
        /* --- 4.1  Parse / default params ---------------------------------- */
        var cfg = new BeaconCfg(
            args.Length > 0 && Guid.TryParse(args[0], out var g) ? g : Guid.Parse("fda50693-a4e2-4fb1-afcf-c6eb07647825"),
            args.Length > 1 && ushort.TryParse(args[1], out var mj) ? mj : (ushort)100,
            args.Length > 2 && ushort.TryParse(args[2], out var mn) ? mn : (ushort)1,
            args.Length > 3 && sbyte.TryParse(args[3], out var tx) ? tx : (sbyte)-59);

        /* --- 4.2  Grab first powered adapter ------------------------------ */
        var adapter = (await BlueZManager.GetAdaptersAsync()).FirstOrDefault()
                   ?? throw new InvalidOperationException("No BLE adapter.");

        if (!await adapter.GetPoweredAsync())
            await adapter.SetPoweredAsync(true);

        /* --- 4.3  Export advert and register with LEAdvertisingManager ---- */
        var system = Connection.System;
        var advert = new BeaconAdvert(cfg);
        await system.RegisterObjectAsync(advert);                     // export

        var advMgr = system.CreateProxy<ILEAdvertisingManager1>("org.bluez", adapter.ObjectPath);
        await advMgr.RegisterAdvertisementAsync(advert.ObjectPath,
                                                new Dictionary<string, object>());

        Console.WriteLine("▲  iBeacon advertising (Ctrl-C to stop)");

        /* --- 4.4  Wait for Ctrl-C ----------------------------------------- */
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        try { await Task.Delay(Timeout.Infinite, cts.Token); }
        catch (OperationCanceledException) { }

        /* --- 4.5  Unregister + clean up ----------------------------------- */
        await advMgr.UnregisterAdvertisementAsync(advert.ObjectPath);
        system.UnregisterObject(advert.ObjectPath);
        Console.WriteLine("■  Advertising stopped – goodbye");
    }
}
