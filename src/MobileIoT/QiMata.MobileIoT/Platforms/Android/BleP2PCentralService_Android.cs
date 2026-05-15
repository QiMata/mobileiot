#if ANDROID && TEST_HARNESS
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using QiMata.MobileIoT.Helpers;
using QiMata.MobileIoT.Services.Interfaces;
using Debug = System.Diagnostics.Debug;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class BleP2PCentralService_Android : IBleP2PCentralService
{
    const string Tag = "BleP2PCentralService_Android";
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

        // OS-level scan filter (serviceUuids) restricts to devices advertising
        // our service UUID; we don't filter by name because BluetoothAdapter.SetName
        // requires BLUETOOTH_PRIVILEGED on Android 12+ and silently no-ops, so the
        // advertised local name is the OEM default rather than `deviceName`.
        var device = await BleScanHelpers.WaitForDeviceAsync(_adapter, serviceUuid, ct, Tag).ConfigureAwait(false);
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
                catch (Exception ex) { Debug.WriteLine($"{Tag}: Read failed: {ex.Message}"); }
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
            catch (Exception ex) { Debug.WriteLine($"{Tag}: Disconnect failed: {ex.Message}"); }
        }
    }
}
#endif
