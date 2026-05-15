using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Diagnostics;

namespace QiMata.MobileIoT.Helpers;

/// <summary>
/// Stateless helpers for the Plugin.BLE scan + discovery flow that's identical
/// across Android and iOS `BleP2PCentralService_*` implementations. Helpers stay
/// static (no virtual hooks) so the recent hardware-tier BLE bug fixes — which
/// turn on exact delegate identity and stop-scan ordering — aren't disturbed
/// by indirection.
/// </summary>
public static class BleScanHelpers
{
    /// <summary>
    /// Starts a service-UUID-filtered scan, returns the first matching device,
    /// then stops the scan. The handler is constructed once locally so the
    /// matching `+=`/`-=` pair targets the same delegate instance.
    /// </summary>
    public static async Task<IDevice?> WaitForDeviceAsync(
        IAdapter adapter,
        Guid serviceUuid,
        CancellationToken cancellationToken,
        string callerTag = "BleScanHelpers")
    {
        var tcs = new TaskCompletionSource<IDevice?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDiscovered(object? sender, DeviceEventArgs args)
        {
            if (args.Device is not null)
                tcs.TrySetResult(args.Device);
        }

        adapter.DeviceDiscovered += OnDiscovered;
        try
        {
            await adapter.StartScanningForDevicesAsync(
                serviceUuids: new[] { serviceUuid },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            try
            {
                return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        finally
        {
            adapter.DeviceDiscovered -= OnDiscovered;
            try { await adapter.StopScanningForDevicesAsync().ConfigureAwait(false); }
            catch (Exception ex) { Debug.WriteLine($"{callerTag}: StopScan failed: {ex.Message}"); }
        }
    }
}
