#if ANDROID && TEST_HARNESS
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using QiMata.MobileIoT.Helpers;
using QiMata.MobileIoT.Services.Interfaces;
using Debug = System.Diagnostics.Debug;
using OperationCanceledException = System.OperationCanceledException;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class BleP2PCentralService_Android : IBleP2PCentralService
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
            // devices advertising our service UUID; we don't filter by name
            // because BluetoothAdapter.SetName needs BLUETOOTH_PRIVILEGED on
            // Android 12+ and silently no-ops, so the advertised local name
            // is the OEM default rather than `deviceName`.
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
            catch (Exception ex) { Debug.WriteLine($"BleP2PCentralService_Android: StopScan failed: {ex.Message}"); }
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
            // Write the payload — the peripheral records `centralBytesReceived`.
            if (target.CanWrite)
            {
                await target.WriteAsync(payload, ct).ConfigureAwait(false);
            }

            // Best-effort read for symmetry; not required for the assertion.
            byte[]? response = payload;
            if (target.CanRead)
            {
                try
                {
                    var (data, code) = await target.ReadAsync(ct).ConfigureAwait(false);
                    if (code == 0 && data is not null && data.Length > 0)
                        response = data;
                }
                catch (Exception ex) { Debug.WriteLine($"BleP2PCentralService_Android: Read failed: {ex.Message}"); }
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
            catch (Exception ex) { Debug.WriteLine($"BleP2PCentralService_Android: Disconnect failed: {ex.Message}"); }
        }
    }
}
#endif
