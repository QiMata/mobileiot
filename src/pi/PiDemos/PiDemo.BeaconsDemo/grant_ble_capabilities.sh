#!/bin/bash
# Grant BLE capabilities to the published binary so it can manage the radio
sudo setcap 'cap_net_raw,cap_net_admin+eip' /opt/pibeacon/PiBleBeacon
