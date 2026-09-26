#!/bin/sh
# Builds Assets/Plugins/macOS/ValleyTrackpad.bundle (universal arm64 + x86_64) from Native/macOS/ValleyTrackpad.m,
# after its checks pass. Needs the Xcode command-line tools. Run it again after editing the plugin source.
set -eu
ROOT=$(cd "$(dirname "$0")/.." && pwd)
SRC="$ROOT/Native/macOS"
OUT="$ROOT/Assets/Plugins/macOS/ValleyTrackpad.bundle"
WORK=$(mktemp -d)
FLAGS="-arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -fobjc-arc -O2 -Wall -Werror"

clang $FLAGS -framework AppKit "$SRC/ValleyTrackpad.m" "$SRC/ValleyTrackpadTests.m" -o "$WORK/tests"
"$WORK/tests"

mkdir -p "$OUT/Contents/MacOS"
clang $FLAGS -bundle -framework AppKit "$SRC/ValleyTrackpad.m" -o "$OUT/Contents/MacOS/ValleyTrackpad"
cat > "$OUT/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key><string>ValleyTrackpad</string>
    <key>CFBundleIdentifier</key><string>com.valleyrail.trackpad</string>
    <key>CFBundleName</key><string>ValleyTrackpad</string>
    <key>CFBundlePackageType</key><string>BNDL</string>
    <key>CFBundleShortVersionString</key><string>1.0</string>
    <key>CFBundleVersion</key><string>1</string>
</dict>
</plist>
PLIST
codesign --force --sign - "$OUT"
rm "$WORK/tests" && rmdir "$WORK"
echo "Built $OUT ($(lipo -archs "$OUT/Contents/MacOS/ValleyTrackpad"))"
