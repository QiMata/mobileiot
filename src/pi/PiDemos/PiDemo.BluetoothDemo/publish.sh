#!/bin/bash
# Build a self-contained, single-file publish for Raspberry Pi (arm64)
dotnet publish -c Release -o publish --self-contained -r linux-arm64
