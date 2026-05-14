#if IOS && TEST_HARNESS
using System.Diagnostics;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using QiMata.MobileIoT.Helpers;
using QiMata.MobileIoT.Services.Interfaces;

namespace QiMata.MobileIoT.Platforms.iOS;

/// <summary>
/// iOS central wrapper for the TestHarness `ble-p2p-central` scenario.
/// Plugin.BLE 3.x handles the central role on iOS via CoreBluetooth under
/// the hood; this wrapper keeps the interface surface symmetric with the
/// Android implementation.
/// </summary>
public sealed class BleP2PCentralService_iOS : IBleP2PCentralService
{
    readonly IBluetoothLE _ble = CrossBluetoothLE.Current;
    readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;

    public async Task<byte[]?> ConnectAndExchangeAsync(
        string deviceName, Guid serviceUuid, byte[] payload,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        await BlePermissions.EnsureAsync().ConfigureAwait(false);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);
        var ct = linkedCts.Token;

        var tcs = new TaskCompletionSource<IDevice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnDiscovered(object? sender, DeviceEventArgs args)
        {
            // OS-level scan filter (serviceUuids below) already restricts to
            // devices advertising our service UUID; matching on name is
            // fragile across stacks (Android can't honor SetName without
            // BLUETOOTH_PRIVILEGED on 12+).
            if (args.Device is not null)
                tcs.TrySetResult(args.Device);
        }

        _adapter.DeviceDiscovered += OnDiscovered;
        IDevice? device = null;
        try
        {
            await _adapter.StartScanningForDevicesAsync(
                serviceUuids: new[] { serviceUuid },
                cancellationToken: ct).ConfigureAwait(false);
            try
            {
                device = await tcs.Task.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        finally
        {
            _adapter.DeviceDiscovered -= OnDiscovered;
            try { await _adapter.StopScanningForDevicesAsync().ConfigureAwait(false); }
            catch (Exception ex) { Debug.WriteLine($"BleP2PCentralService_iOS: StopScan failed: {ex.Message}"); }
        }

        if (device is null) return null;

        try
        {
            await _adapter.ConnectToDeviceAsync(device, cancellationToken: ct).ConfigureAwait(false);
            var service = await device.GetServiceAsync(serviceUuid, ct).ConfigureAwait(false);
            if (service is null) return null;

            var characteristics = await service.GetCharacteristicsAsync().ConfigureAwait(false);
            if (characteristics is null || characteristics.Count == 0) return null;

            var target = characteristics[0];
            if (target.CanWrite)
            {
                await target.WriteAsync(payload, ct).ConfigureAwait(false);
            }

            byte[]? response = payload;
            if (target.CanRead)
            {
                try
                {
                    var (data, code) = await target.ReadAsync(ct).ConfigureAwait(false);
                    if (code == 0 && data is not null && data.Length > 0)
                        response = data;
                }
                catch (Exception ex) { Debug.WriteLine($"BleP2PCentralService_iOS: Read failed: {ex.Message}"); }
            }
            return response;
        }
        finally
        {
            try
            {
                if (_adapter.ConnectedDevices.Contains(device))
                    await _adapter.DisconnectDeviceAsync(device).ConfigureAwait(false);
            }
            catch (Exception ex) { Debug.WriteLine($"BleP2PCentralService_iOS: Disconnect failed: {ex.Message}"); }
        }
    }
}
#endif
