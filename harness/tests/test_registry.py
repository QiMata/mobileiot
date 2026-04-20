from __future__ import annotations

import pytest

from harness.inventory.model import Device, DeviceKind, DeviceUnavailable
from harness.inventory.registry import Inventory


@pytest.mark.mock
def test_find_filters_by_kind_and_caps():
    inv = Inventory([
        Device(id="a", kind=DeviceKind.ANDROID, online=True, capabilities={"ble"}),
        Device(id="b", kind=DeviceKind.ANDROID, online=True, capabilities={"nfc"}),
        Device(id="c", kind=DeviceKind.PI, online=True, capabilities={"ble"}),
    ])
    got = inv.find(kind=DeviceKind.ANDROID, capabilities=["ble"])
    assert [d.id for d in got] == ["a"]


@pytest.mark.mock
def test_require_raises_with_structured_reason():
    inv = Inventory([
        Device(id="a", kind=DeviceKind.ANDROID, online=False, reason_offline="unplugged", capabilities={"ble"}),
    ])
    with pytest.raises(DeviceUnavailable) as exc:
        inv.require(role="phone", kind=DeviceKind.ANDROID, capabilities=["ble"])
    assert "unplugged" in exc.value.reason


@pytest.mark.mock
def test_summary_counts_by_kind():
    inv = Inventory([
        Device(id="a", kind=DeviceKind.ANDROID, online=True),
        Device(id="b", kind=DeviceKind.ANDROID, online=False, reason_offline="x"),
        Device(id="p", kind=DeviceKind.PI, online=True),
    ])
    s = inv.summary()
    assert s["by_kind"]["android"] == {"configured": 2, "online": 1}
    assert s["by_kind"]["pi"] == {"configured": 1, "online": 1}
    assert s["total"] == 3
