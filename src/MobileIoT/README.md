# MobileIoT Demo Apps

This solution contains two independent demo apps plus one shared harness/contracts library.

## Projects

- `QiMata.MobileIoT.Shared` is a platform-neutral `net8.0` library. It owns service interfaces, models, mock services, Thread service/model logic, and the TEST_HARNESS HTTP/scenario contract.
- `QiMata.MobileIoT` is the existing .NET MAUI demo. It keeps MAUI pages, MAUI ViewModels, Shell navigation, MAUI permissions, and MAUI/platform service implementations.
- `QiMata.MobileIoT.Uno` is the Uno Platform demo. It uses a WinUI-style `Frame`, Uno pages, Uno service registrations, and the same shared harness host.

The app ids are:

- MAUI: `com.qimata.mobileiot`
- Uno: `com.qimata.mobileiot.uno`

## Harness Contract

Both apps expose the same localhost-only `HarnessHttpHost` when built with `TestHarness`. The Python harness drives `/health`, `/scenario/{name}`, `/state`, `/inject`, and `/logs` without knowing which UI framework is running.

Shared scenarios live in `QiMata.MobileIoT.Shared/Services/TestHarness/Scenarios`. They return a common state shape with `status` values of `running`, `passed`, `failed`, or `skipped`; skipped states include a reason.

## Common Commands

```bash
dotnet build src/MobileIoT/QiMata.MobileIoT.Shared/QiMata.MobileIoT.Shared.csproj -c TestHarness
dotnet build src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj -c TestHarness -f net9.0-windows10.0.19041
python -m harness build --app uno --platform windows
python -m harness run --integration smoke_uno_windows --app uno --tier hardware
```

MAUI mobile builds still target the existing `net8.0-android` and `net8.0-ios` heads. On .NET 10 hosts, those TFMs may require workload/package pinning because .NET 8 mobile workloads are now out of support.
