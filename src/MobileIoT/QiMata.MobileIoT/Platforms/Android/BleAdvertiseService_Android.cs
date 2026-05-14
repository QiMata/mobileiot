#if ANDROID && TEST_HARNESS
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.OS;
using Java.Util;
using QiMata.MobileIoT.Services.Interfaces;
using Debug = System.Diagnostics.Debug;
using OperationCanceledException = System.OperationCanceledException;

namespace QiMata.MobileIoT.Platforms.Android;

public sealed class BleAdvertiseService_Android : IBleAdvertiseService
{
    static readonly UUID CharUuid = UUID.FromString("00002a37-0000-1000-8000-00805f9b34fb");

    BluetoothManager? _manager;
    BluetoothGattServer? _gattServer;
    BluetoothLeAdvertiser? _advertiser;
    AdvertiseCallback? _advCallback;
    BluetoothGattServerCallback? _gattCallback;
    BluetoothGattCharacteristic? _characteristic;
    TaskCompletionSource<bool>? _startTcs;
    byte[] _payload = Array.Empty<byte>();

    public bool IsAvailable
    {
        get
        {
            var ctx = global::Android.App.Application.Context;
            var bm = (BluetoothManager?)ctx.GetSystemService(global::Android.Content.Context.BluetoothService);
            var adapter = bm?.Adapter;
            return adapter is not null && adapter.IsEnabled && adapter.BluetoothLeAdvertiser is not null;
        }
    }

    public event EventHandler<byte[]>? CharacteristicWritten;
    public event EventHandler? CharacteristicRead;

    public async Task StartAsync(Guid serviceUuid, byte[] payload, string deviceName, CancellationToken cancellationToken)
    {
        var ctx = global::Android.App.Application.Context;
        _manager = (BluetoothManager?)ctx.GetSystemService(global::Android.Content.Context.BluetoothService);
        var adapter = _manager?.Adapter
            ?? throw new InvalidOperationException("No Bluetooth adapter");
        if (!adapter.IsEnabled)
            throw new InvalidOperationException("Bluetooth adapter is not enabled");

        _payload = payload ?? Array.Empty<byte>();

        // Don't try to mutate the system adapter name (`adapter.SetName`)
        // — that's the phone's global Bluetooth name and requires
        // BLUETOOTH_PRIVILEGED on Android 12+. The central side matches by
        // service UUID, which is included in the advertisement below.

        var serviceUuidJava = UUID.FromString(serviceUuid.ToString());
        var service = new BluetoothGattService(ParcelUuid.FromString(serviceUuidJava.ToString())!.Uuid, GattServiceType.Primary);
        _characteristic = new BluetoothGattCharacteristic(
            CharUuid,
            GattProperty.Read | GattProperty.Write,
            GattPermission.Read | GattPermission.Write);
        _characteristic.SetValue(_payload);
        service.AddCharacteristic(_characteristic);

        _gattCallback = new ServerCallback(this);
        _gattServer = _manager!.OpenGattServer(ctx, _gattCallback);
        if (_gattServer is null)
            throw new InvalidOperationException("OpenGattServer returned null");
        _gattServer.AddService(service);

        _advertiser = adapter.BluetoothLeAdvertiser
            ?? throw new InvalidOperationException("BluetoothLeAdvertiser unavailable on this OEM stack");

        var settings = new AdvertiseSettings.Builder()
            ?.SetAdvertiseMode(AdvertiseMode.LowLatency)
            ?.SetTxPowerLevel(AdvertiseTx.PowerHigh)
            ?.SetConnectable(true)
            ?.Build()
            ?? throw new InvalidOperationException("Could not build AdvertiseSettings");

        var data = new AdvertiseData.Builder()
            ?.SetIncludeDeviceName(true)
            ?.AddServiceUuid(new ParcelUuid(serviceUuidJava))
            ?.Build()
            ?? throw new InvalidOperationException("Could not build AdvertiseData");

        _startTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _advCallback = new AdvCallback(_startTcs);
        _advertiser.StartAdvertising(settings, data, _advCallback);

        // Wait briefly for OnStartSuccess or OnStartFailure so the scenario can
        // surface a meaningful skip reason instead of hanging.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await _startTcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Treat as advertise failure — clean up and rethrow so scenario reports skipped.
            await StopAsync().ConfigureAwait(false);
            throw new InvalidOperationException("BLE advertise did not start within 5s (OEM stack may have rejected it)");
        }
    }

    public Task StopAsync()
    {
        try
        {
            if (_advertiser is not null && _advCallback is not null)
                _advertiser.StopAdvertising(_advCallback);
        }
        catch (Exception ex) { Debug.WriteLine($"BleAdvertiseService_Android: StopAdvertising failed: {ex.Message}"); }

        try { _gattServer?.Close(); }
        catch (Exception ex) { Debug.WriteLine($"BleAdvertiseService_Android: gattServer.Close failed: {ex.Message}"); }

        _advertiser = null;
        _advCallback = null;
        _gattServer = null;
        _gattCallback = null;
        _characteristic = null;
        return Task.CompletedTask;
    }

    void RaiseWritten(byte[] bytes) => CharacteristicWritten?.Invoke(this, bytes);
    void RaiseRead() => CharacteristicRead?.Invoke(this, EventArgs.Empty);

    sealed class AdvCallback : AdvertiseCallback
    {
        readonly TaskCompletionSource<bool> _tcs;
        public AdvCallback(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public override void OnStartSuccess(AdvertiseSettings? settingsInEffect)
        {
            _tcs.TrySetResult(true);
        }

        public override void OnStartFailure(AdvertiseFailure errorCode)
        {
            _tcs.TrySetException(new InvalidOperationException(
                $"BLE advertise failed with code {errorCode}"));
        }
    }

    sealed class ServerCallback : BluetoothGattServerCallback
    {
        readonly BleAdvertiseService_Android _owner;
        public ServerCallback(BleAdvertiseService_Android owner) => _owner = owner;

        public override void OnCharacteristicReadRequest(
            BluetoothDevice? device, int requestId, int offset,
            BluetoothGattCharacteristic? characteristic)
        {
            try
            {
                _owner._gattServer?.SendResponse(device, requestId, GattStatus.Success, offset, _owner._payload);
                _owner.RaiseRead();
            }
            catch (Exception ex) { Debug.WriteLine($"BleAdvertiseService_Android: OnRead failed: {ex.Message}"); }
        }

        public override void OnCharacteristicWriteRequest(
            BluetoothDevice? device, int requestId,
            BluetoothGattCharacteristic? characteristic,
            bool preparedWrite, bool responseNeeded,
            int offset, byte[]? value)
        {
            try
            {
                if (responseNeeded)
                    _owner._gattServer?.SendResponse(device, requestId, GattStatus.Success, offset, value);
                if (value is not null && value.Length > 0)
                    _owner.RaiseWritten(value);
            }
            catch (Exception ex) { Debug.WriteLine($"BleAdvertiseService_Android: OnWrite failed: {ex.Message}"); }
        }
    }
}
#endif
