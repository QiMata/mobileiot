// File: Program.cs
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Linux.Bluetooth;

namespace PiBleBeacon
{
    internal sealed record BeaconConfig(Guid Uuid, ushort Major, ushort Minor, sbyte TxPower);

    internal sealed class BeaconAdvertiser : IAsyncDisposable
    {
        private readonly BeaconConfig _cfg;
        private ILEAdvertisement? _handle;

        public BeaconAdvertiser(BeaconConfig cfg) => _cfg = cfg;

        public async Task StartAsync(CancellationToken ct = default)
        {
            await BlueZManager.InitializeAsync(ct);

            var adapter = await BlueZManager.GetFirstAdapterAsync(ct)
                         ?? throw new InvalidOperationException("No Bluetooth adapter found.");

            if (!await adapter.GetPoweredAsync(ct))
                await adapter.SetPoweredAsync(true, ct);

            var advMgr  = await adapter.GetLEAdvertisingManagerAsync(ct);
            var payload = BuildIBeaconPayload(_cfg);

            var adv = new LEAdvertisement { Type = LEAdvertisingType.Broadcast };
            adv.ManufacturerData.Add(0x004C, payload);   // Apple company ID

            _handle = await advMgr.RegisterAdvertisementAsync(adv, ct);
            Console.WriteLine("iBeacon advertising started.");
        }

        public async Task StopAsync()
        {
            if (_handle is null) return;
            await _handle.UnregisterAsync();
            _handle = null;
        }

        public async ValueTask DisposeAsync() => await StopAsync();

        private static byte[] BuildIBeaconPayload(BeaconConfig c)
        {
            Span<byte> buf = stackalloc byte[23];
            buf[0] = 0x02; buf[1] = 0x15;                       // iBeacon prefix

            Span<byte> le = stackalloc byte[16];
            c.Uuid.TryWriteBytes(le);

            Span<byte> be = stackalloc byte[16];                // convert to big-endian
            be[0]=le[3]; be[1]=le[2]; be[2]=le[1]; be[3]=le[0];
            be[4]=le[5]; be[5]=le[4]; be[6]=le[7]; be[7]=le[6];
            le[8..].CopyTo(be[8..]);

            be.CopyTo(buf[2..]);

            buf[18] = (byte)(c.Major >> 8);
            buf[19] = (byte)(c.Major & 0xFF);
            buf[20] = (byte)(c.Minor >> 8);
            buf[21] = (byte)(c.Minor & 0xFF);
            buf[22] = unchecked((byte)c.TxPower);

            return buf.ToArray();
        }
    }

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var uuid  = args.Length > 0 && Guid.TryParse(args[0], out var u) ? u
                       : Guid.Parse("fda50693-a4e2-4fb1-afcf-c6eb07647825");
            var major = args.Length > 1 && ushort.TryParse(args[1], out var ma) ? ma : (ushort)100;
            var minor = args.Length > 2 && ushort.TryParse(args[2], out var mi) ? mi : (ushort)1;
            var tx    = args.Length > 3 && sbyte.TryParse(args[3], out var txp) ? txp : (sbyte)-59;

            var cfg = new BeaconConfig(uuid, major, minor, tx);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            await using var adv = new BeaconAdvertiser(cfg);
            await adv.StartAsync(cts.Token);

            try     { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch   (OperationCanceledException) { /* Ctrl-C */ }

            await adv.StopAsync();
            Console.WriteLine("Beacon advertising stopped.");
        }
    }
}

