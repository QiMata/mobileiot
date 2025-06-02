# Pi Bluetooth GATT Demo

This project exposes a BLE service on a Raspberry Pi with:

- Temperature and humidity from a DHT22 sensor (read/notify)
- A writable LED toggle
- Advertises as `PiDHTSensor`

It relies on the [`Linux.Bluetooth`](https://www.nuget.org/packages/Linux.Bluetooth) D-Bus wrapper and the .NET IoT packages.

## Building

```bash
# restore and publish a single-file binary
dotnet publish -c Release -o publish --self-contained -r linux-arm64
```

Grant the Bluetooth capabilities so the process can use the adapter without running as root:

```bash
sudo setcap 'cap_net_raw,cap_net_admin+eip' publish/PiBleDemo
```

### Optional: systemd unit

```ini
[Service]
ExecStart=/opt/pible/PiBleDemo
Restart=always
User=pi
AmbientCapabilities=CAP_NET_RAW CAP_NET_ADMIN
```

## Client contract

| Item                   | UUID                                   | Properties     | Notes                           |
| ---------------------- | -------------------------------------- | -------------- | ------------------------------- |
| **Service**            | `12345678-1234-1234-1234-1234567890AB` | -              | root container                  |
| Temperature            | `00002A6E-0000-1000-8000-00805F9B34FB` | read, notify   | Int16 in hundredths °C          |
| Humidity               | `00002A6F-0000-1000-8000-00805F9B34FB` | read, notify   | UInt16 in hundredths %RH        |
| LED control            | `12345679-1234-1234-1234-1234567890AB` | write          | 1 byte: 0=off, 1=on             |
```
