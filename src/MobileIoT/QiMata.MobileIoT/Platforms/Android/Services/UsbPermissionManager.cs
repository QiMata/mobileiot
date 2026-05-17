#if ANDROID
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using QiMata.MobileIoT.Shared.Services.Interfaces;
using System.Collections.Concurrent;

namespace QiMata.MobileIoT.Platforms.Android.Services;

/// <summary>
/// Tracks in-flight USB permission requests. Entries are pruned on completion
/// and on disposal so cancelled/timed-out requests don't leak.
/// </summary>
public sealed class UsbPermissionManager : IDisposable
{
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> InFlight = new();
    private readonly IAppLogger _logger;

    public UsbPermissionManager(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnsurePermissionAsync(UsbManager usb, UsbDevice device, CancellationToken ct = default)
    {
        if (usb.HasPermission(device)) return true;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        InFlight[device.DeviceId] = tcs;

        try
        {
            var pi = PendingIntent.GetBroadcast(
                Application.Context, 0,
                new Intent(UsbPermissionBroadcastReceiver.ACTION_USB_PERMISSION),
                PendingIntentFlags.Immutable);

            usb.RequestPermission(device, pi);

            using var registration = ct.Register(() =>
            {
                if (InFlight.TryRemove(device.DeviceId, out var pending))
                    pending.TrySetCanceled(ct);
            });

            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            InFlight.TryRemove(device.DeviceId, out _);
            throw;
        }
    }

    /// <summary>Invoked by <see cref="UsbPermissionBroadcastReceiver"/> when Android delivers a result.</summary>
    internal static void CompletePermission(int deviceId, bool granted)
    {
        if (InFlight.TryRemove(deviceId, out var tcs))
            tcs.TrySetResult(granted);
    }

    public void Dispose()
    {
        foreach (var kvp in InFlight)
        {
            if (InFlight.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }
        _logger.Debug("UsbPermissionManager disposed");
    }
}
#endif
