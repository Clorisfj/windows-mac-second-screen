#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if ! xcrun --find swiftc >/dev/null 2>&1; then
  echo '需要 Apple Command Line Tools。请先在终端运行 xcode-select --install，完成后再运行本文件。'
  exit 1
fi
APP="$PWD/dist/CFlow.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
xcrun swiftc -swift-version 5 -O -target "$(uname -m)-apple-macos14.0" Sources/*.swift \
  -framework SwiftUI -framework AppKit -framework Network -framework Security \
  -framework CryptoKit -framework ServiceManagement -o "$APP/Contents/MacOS/CFlow"
cp Info.plist "$APP/Contents/Info.plist"
cp ../assets/CFlow.png "$APP/Contents/Resources/CFlow.png"
ICONSET="$PWD/dist/CFlow.iconset"
mkdir -p "$ICONSET"
for n in 16 32 128 256 512; do
  sips -z "$n" "$n" ../assets/CFlow.png --out "$ICONSET/icon_${n}x${n}.png" >/dev/null
  double=$((n*2))
  sips -z "$double" "$double" ../assets/CFlow.png --out "$ICONSET/icon_${n}x${n}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/CFlow.icns"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
echo "构建完成：$APP"
echo '将 CFlow.app 放到 /Applications，再打开；菜单栏出现双屏 C 图标后，在设置中复制配对码。'
