#!/bin/sh

set -eux

for config in Release Debug; do
  for platform in "Any CPU" x86 x64; do
    msbuild /p:Configuration=$config /p:Platform="$platform" OpenVpnService.sln
  done
done
