from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

AppName = Literal["maui", "uno"]
Platform = Literal["android", "ios", "windows"]


@dataclass(frozen=True)
class BuildSpec:
    app: AppName
    platform: Platform
    csproj_rel: str
    tfm: str
    extension: str
    package_id: str
    publish: bool = False
    runtime: str | None = None
    exe_name: str | None = None


BUILD_SPECS: dict[tuple[AppName, Platform], BuildSpec] = {
    ("maui", "android"): BuildSpec(
        "maui",
        "android",
        "src/MobileIoT/QiMata.MobileIoT/QiMata.MobileIoT.csproj",
        "net8.0-android",
        ".apk",
        "com.qimata.mobileiot",
    ),
    ("maui", "ios"): BuildSpec(
        "maui",
        "ios",
        "src/MobileIoT/QiMata.MobileIoT/QiMata.MobileIoT.csproj",
        "net8.0-ios",
        ".ipa",
        "com.qimata.mobileiot",
    ),
    ("uno", "windows"): BuildSpec(
        "uno",
        "windows",
        "src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj",
        "net9.0-windows10.0.19041",
        ".exe",
        "com.qimata.mobileiot.uno",
        publish=True,
        exe_name="QiMata.MobileIoT.Uno.exe",
    ),
    ("uno", "android"): BuildSpec(
        "uno",
        "android",
        "src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj",
        "net9.0-android",
        ".apk",
        "com.qimata.mobileiot.uno",
    ),
    ("uno", "ios"): BuildSpec(
        "uno",
        "ios",
        "src/MobileIoT/QiMata.MobileIoT.Uno/QiMata.MobileIoT.Uno.csproj",
        "net9.0-ios",
        ".ipa",
        "com.qimata.mobileiot.uno",
    ),
}
