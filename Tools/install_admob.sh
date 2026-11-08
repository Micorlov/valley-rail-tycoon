#!/bin/bash
# Fetches the Google Mobile Ads Unity plugin and unpacks it into this project. The plugin is not committed;
# only Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset (the AdMob app ID) is.
# Run once per checkout, before the Android build (Valley Rail -> Build Android APK), which defines ADMOB_SDK.
#
#   Tools/install_admob.sh [path/to/GoogleMobileAds-v11.6.0.unitypackage]
set -euo pipefail

VERSION=11.6.0
SHA256=e42c4af9284a6efa3e925ecb58047b722e07c12ac28dceb4547bb743f3db4763
URL="https://github.com/googleads/googleads-mobile-unity/releases/download/v${VERSION}/GoogleMobileAds-v${VERSION}.unitypackage"

HERE="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PACKAGE="${1:-$WORK/GoogleMobileAds.unitypackage}"
if [ ! -f "$PACKAGE" ]; then
  echo "Downloading $URL"
  curl -fL -o "$PACKAGE" "$URL"
fi

echo "Verifying checksum"
echo "$SHA256  $PACKAGE" | shasum -a 256 -c -

echo "Unpacking into Assets"
python3 -I "$HERE/Tools/unpack_unitypackage.py" "$HERE" "$PACKAGE"

echo "Google Mobile Ads imported."
