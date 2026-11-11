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
SOURCE_DIMENSIONS="$(sips -g pixelWidth -g pixelHeight ../assets/CFlow.png)"
SOURCE_WIDTH="$(echo "$SOURCE_DIMENSIONS" | awk '/pixelWidth/ {print $2}')"
SOURCE_HEIGHT="$(echo "$SOURCE_DIMENSIONS" | awk '/pixelHeight/ {print $2}')"
ICON_EDGE="$SOURCE_WIDTH"
if [ "$SOURCE_HEIGHT" -lt "$SOURCE_WIDTH" ]; then
  ICON_EDGE="$SOURCE_HEIGHT"
fi
ICON_SOURCE="$PWD/dist/CFlow-square.png"
sips --cropToHeightWidth "$ICON_EDGE" "$ICON_EDGE" ../assets/CFlow.png --out "$ICON_SOURCE" >/dev/null
cp "$ICON_SOURCE" "$APP/Contents/Resources/CFlow.png"
ICONSET="$PWD/dist/CFlow.iconset"
mkdir -p "$ICONSET"
for n in 16 32 128 256 512; do
  sips -z "$n" "$n" "$ICON_SOURCE" --out "$ICONSET/icon_${n}x${n}.png" >/dev/null
  double=$((n*2))
  sips -z "$double" "$double" "$ICON_SOURCE" --out "$ICONSET/icon_${n}x${n}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/CFlow.icns"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
echo "构建完成：$APP"
echo '将 CFlow.app 放到 /Applications，再打开；菜单栏出现双屏 C 图标后，在设置中查看 IP 并开启允许配对。'
