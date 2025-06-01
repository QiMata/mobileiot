# Pi Bluetooth GATT Demo

This project exposes a BLE service on a Raspberry Pi with:

- Temperature and humidity from a DHT22 sensor (read/notify)
- A writable LED toggle

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
| **Service**            | `e95d93b0-251d-470a-a062-fa1922dfa9a8` | –              | root container                  |
| Temperature + Humidity | `e95d9250-251d-470a-a062-fa1922dfa9a8` | read, notify   | `float T`, `float RH`           |
| LED control            | `e95d93ee-251d-470a-a062-fa1922dfa9a8` | write          | 1 byte: 0=off, 1=on             |
```
