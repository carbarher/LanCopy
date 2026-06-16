#!/usr/bin/env bash
# Crea un bundle .app minimo para LanCopy en macOS.
# Uso: make-app.sh <ruta-al-binario> <version>   (e.g. make-app.sh publish/osx-arm64/LanCopy 1.0.0)
set -euo pipefail
BIN="${1:?Falta ruta al binario}"
VER="${2:-1.0.0}"

APP="LanCopy.app"
CONTENTS="$APP/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"

rm -rf "$APP"
mkdir -p "$MACOS" "$RESOURCES"

cp "$BIN" "$MACOS/LanCopy"
chmod +x "$MACOS/LanCopy"

# Icono: convierte app.png a .icns si sips/iconutil disponibles, si no copia png
if command -v sips &>/dev/null && command -v iconutil &>/dev/null; then
  ICONSET="$RESOURCES/LanCopy.iconset"
  mkdir -p "$ICONSET"
  for size in 16 32 128 256 512; do
    sips -z $size $size Assets/app.png --out "$ICONSET/icon_${size}x${size}.png" &>/dev/null
    sips -z $((size*2)) $((size*2)) Assets/app.png --out "$ICONSET/icon_${size}x${size}@2x.png" &>/dev/null
  done
  iconutil -c icns -o "$RESOURCES/LanCopy.icns" "$ICONSET"
  rm -rf "$ICONSET"
  ICON_KEY="<key>CFBundleIconFile</key><string>LanCopy</string>"
else
  cp Assets/app.png "$RESOURCES/LanCopy.png"
  ICON_KEY=""
fi

cat > "$CONTENTS/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>LanCopy</string>
  <key>CFBundleDisplayName</key><string>LanCopy</string>
  <key>CFBundleIdentifier</key><string>com.carbarher.lancopy</string>
  <key>CFBundleVersion</key><string>$VER</string>
  <key>CFBundleShortVersionString</key><string>$VER</string>
  <key>CFBundleExecutable</key><string>LanCopy</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  $ICON_KEY
</dict></plist>
PLIST

echo "Creado: $APP"