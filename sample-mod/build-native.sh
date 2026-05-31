#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

x86_64-w64-mingw32-gcc \
  -shared \
  -O2 \
  -o sample-mod/native/mlhook1.dll \
  sample-mod/native/mlhook1.c \
  -lkernel32 \
  -Wl,--kill-at

cp -f sample-mod/native/mlhook1.dll "Majdata Hub/game/mlhook1.dll"
echo "Built and installed Majdata Hub/game/mlhook1.dll"
