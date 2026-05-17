#if IOS && TEST_HARNESS
using System.Diagnostics;
using CoreBluetooth;
using CoreFoundation;
using Foundation;
using QiMata.MobileIoT.Constants;
using QiMata.MobileIoT.Shared.Services.Interfaces;

namespace QiMata.MobileIoT.Platforms.iOS;

/// <summary>
/// Native CBPeripheralManager wrapper exposing the TestHarness
/// `IBleAdvertiseService` surface so an iPhone can play the BLE peripheral
/// role in the `ble-p2p` integration suite.
///
/// Plugin.BLE 3.x does not implement the peripheral role on iOS — this
/// thin NSObject delegate fills that gap.
/// </summary>
public sealed class BleAdvertiseService_iOS : NSObject, IBleAdvertiseService, ICBPeripheralManagerDelegate
{
    static readonly CBUUID CharUuid = CBUUID.FromString("00002A37-0000-1000-8000-00805F9B34FB");

    CBPeripheralManager? _manager;
    CBMutableService? _service;
    CBMutableCharacteristic? _characteristic;
    byte[] _payload = Array.Empty<byte>();
    string _deviceName = "MobileIotPeripheral";
    CBUUID? _serviceUuid;
    TaskCompletionSource<bool>? _readyTcs;
    bool _started;

    public bool IsAvailable => _manager?.State == CBManagerState.PoweredOn || _manager is null;

    public event EventHandler<byte[]>? CharacteristicWritten;
    public event EventHandler? CharacteristicRead;

    public Task StartAsync(Guid serviceUuid, byte[] payload, string deviceName, CancellationToken cancellationToken)
    {
        _payload = payload ?? Array.Empty<byte>();
        _deviceName = string.IsNullOrEmpty(deviceName) ? "MobileIotPeripheral" : deviceName;
        _serviceUuid = CBUUID.FromString(serviceUuid.ToString());
        _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Initialising the manager triggers the UpdatedState callback on the
        // dispatch queue we provide; the actual AddService + StartAdvertising
        // happens there once state == PoweredOn.
        var queue = DispatchQueue.MainQueue;
        _manager = new CBPeripheralManager(this, queue);

        // Resolve immediately if already powered on (rare on cold start but
        // possible on subsequent invocations).
        if (_manager.State == CBManagerState.PoweredOn)
        {
            ConfigureAndAdvertise();
        }

        // Bound the wait so the scenario surfaces a clean skip on unauthorized
        // / unsupported devices.
        return WaitForReadyAsync(cancellationToken);
    }

    async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(BleConstants.AdvertiseStartTimeoutIos);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await _readyTcs!.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await StopAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"CBPeripheralManager did not reach PoweredOn within {BleConstants.AdvertiseStartTimeoutIos.TotalSeconds:F0}s; advertise aborted");
        }
    }

    void ConfigureAndAdvertise()
    {
        if (_started || _manager is null || _serviceUuid is null) return;
        try
        {
            _characteristic = new CBMutableCharacteristic(
                CharUuid,
                CBCharacteristicProperties.Read | CBCharacteristicProperties.Write,
                value: null,
                permissions: CBAttributePermissions.Readable | CBAttributePermissions.Writeable);

            _service = new CBMutableService(_serviceUuid, primary: true)
            {
                Characteristics = new[] { _characteristic }
            };
            _manager.AddService(_service);

            var opts = new StartAdvertisingOptions
            {
                LocalName = _deviceName,
                ServicesUUID = new[] { _serviceUuid }
            };
            _manager.StartAdvertising(opts);
            _started = true;
            _readyTcs?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BleAdvertiseService_iOS: ConfigureAndAdvertise failed: {ex}");
            _readyTcs?.TrySetException(ex);
        }
    }

    public Task StopAsync()
    {
        try { _manager?.StopAdvertising(); }
        catch (Exception ex) { Debug.WriteLine($"BleAdvertiseService_iOS: StopAdvertising failed: {ex.Message}"); }
        try { _manager?.RemoveAllServices(); }
        catch (Exception ex) { Debug.WriteLine($"BleAdvertiseService_iOS: RemoveAllServices failed: {ex.Message}"); }
        _started = false;
        _service = null;
        _characteristic = null;
        return Task.CompletedTask;
    }

    // ---- CBPeripheralManagerDelegate ----

    [Export("peripheralManagerDidUpdateState:")]
    public void StateUpdated(CBPeripheralManager peripheral)
    {
        Debug.WriteLine($"BleAdvertiseService_iOS: state -> {peripheral.State}");
        switch (peripheral.State)
        {
            case CBManagerState.PoweredOn:
                ConfigureAndAdvertise();
                break;
            case CBManagerState.Unsupported:
            case CBManagerState.Unauthorized:
                _readyTcs?.TrySetException(new InvalidOperationException(
                    $"CBPeripheralManager state is {peripheral.State}"));
                break;
            default:
                break;
        }
    }

    [Export("peripheralManager:didReceiveReadRequest:")]
    public void DidReceiveReadRequest(CBPeripheralManager peripheral, CBATTRequest request)
    {
        try
        {
            if (request.Characteristic.UUID.Equals(CharUuid))
            {
                var data = NSData.FromArray(_payload);
                if ((nint)request.Offset > (nint)data.Length)
                {
                    peripheral.RespondToRequest(request, CBATTError.InvalidOffset);
                    return;
                }
                request.Value = NSData.FromArray(_payload).Subdata(new NSRange(
                    (nint)request.Offset,
                    (nint)(data.Length - request.Offset)));
                peripheral.RespondToRequest(request, CBATTError.Success);
                CharacteristicRead?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                peripheral.RespondToRequest(request, CBATTError.RequestNotSupported);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BleAdvertiseService_iOS: DidReceiveReadRequest failed: {ex}");
            peripheral.RespondToRequest(request, CBATTError.UnlikelyError);
        }
    }

    [Export("peripheralManager:didReceiveWriteRequests:")]
    public void DidReceiveWriteRequests(CBPeripheralManager peripheral, CBATTRequest[] requests)
    {
        try
        {
            foreach (var req in requests)
            {
                if (!req.Characteristic.UUID.Equals(CharUuid)) continue;
                var bytes = req.Value?.ToArray() ?? Array.Empty<byte>();
                if (bytes.Length > 0)
                    CharacteristicWritten?.Invoke(this, bytes);
            }
            if (requests.Length > 0)
                peripheral.RespondToRequest(requests[0], CBATTError.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BleAdvertiseService_iOS: DidReceiveWriteRequests failed: {ex}");
            if (requests.Length > 0)
                peripheral.RespondToRequest(requests[0], CBATTError.UnlikelyError);
        }
    }
}
#endif
