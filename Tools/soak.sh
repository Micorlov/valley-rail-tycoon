#!/usr/bin/env bash
# Device soak for the release gate in PLAN.md milestone 10.
#
# Launches the installed build, starts the three-line demo at 4x, and samples memory, battery and
# temperature every minute for the requested number of minutes (default 30). Development builds also
# log a "VR_PERF" line every 30 s with frame rate, simulation time per frame and managed-heap figures;
# those lines are collected at the end. Results land in Logs/soak-<stamp>.csv and Logs/soak-<stamp>-perf.txt.
#
# Usage: Tools/soak.sh [minutes]          (needs adb on PATH or ADB=/path/to/adb)
# The tap positions are fractions of the landscape screen and match the title menu layout.
set -uo pipefail
cd "$(dirname "$0")/.."
ADB="${ADB:-adb}"
PKG=com.valleyrail.tycoon
# Over Wi-Fi (ANDROID_SERIAL=ip:port) the link can drop while the phone idles; reconnect instead of aborting the run.
reconnect() {
  if ! "$ADB" get-state >/dev/null 2>&1 && [[ "${ANDROID_SERIAL:-}" == *:* ]]; then
    "$ADB" connect "$ANDROID_SERIAL" >/dev/null 2>&1 || true
    sleep 3
  fi
}
MINUTES="${1:-30}"
STAMP="$(date +%Y%m%d-%H%M)"
CSV="Logs/soak-$STAMP.csv"
PERF="Logs/soak-$STAMP-perf.txt"
mkdir -p Logs

read -r W H < <("$ADB" shell wm size | awk '/Physical size/{split($3,a,"x"); if (a[1]>a[2]) print a[1], a[2]; else print a[2], a[1]}')
tap() { "$ADB" shell input tap "$(awk -v w="$W" -v f="$1" 'BEGIN{printf "%d", w*f}')" "$(awk -v h="$H" -v f="$2" 'BEGIN{printf "%d", h*f}')"; }

"$ADB" shell am force-stop "$PKG"
"$ADB" logcat -c
"$ADB" shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
sleep 10
tap 0.5 0.348      # title menu: EXPLORE A WORKING RAILWAY (second button)
sleep 4
tap 0.901 0.083    # HUD speed button: 1x -> 2x
sleep 1
tap 0.901 0.083    # HUD speed button: 2x -> 4x
echo "elapsed_s,pss_kb,rss_kb,battery_pct,temp_c,plugged" > "$CSV"
END=$((SECONDS + MINUTES * 60))
while (( SECONDS < END )); do
  reconnect
  if ! "$ADB" get-state >/dev/null 2>&1; then echo "$SECONDS,LINK DOWN" | tee -a "$CSV"; sleep 30; continue; fi
  PSS=$("$ADB" shell dumpsys meminfo "$PKG" | awk '/TOTAL PSS:/{print $3}')
  RSS=$("$ADB" shell dumpsys meminfo "$PKG" | awk '/TOTAL RSS:/{print $6}')
  # Only the first "level:"/"temperature:" lines are the current values; some vendors append a history below them.
  BATTERY=$("$ADB" shell dumpsys battery)
  BAT=$(echo "$BATTERY" | awk '/^ *level:/{print $2; exit}' | tr -d '\r')
  TEMP=$(echo "$BATTERY" | awk '/^ *temperature:/{printf "%.1f", $2/10; exit}')
  # 1 while any charger is attached, 0 on battery; battery drain is only meaningful in the 0 rows.
  PLUG=$(echo "$BATTERY" | awk '/^ *(AC|USB|Wireless) powered: true/{p=1} END{print p+0}')
  echo "$SECONDS,$PSS,$RSS,$BAT,$TEMP,$PLUG" | tee -a "$CSV"
  if ! "$ADB" shell pidof "$PKG" >/dev/null; then echo "APP EXITED" | tee -a "$CSV"; break; fi
  sleep 60
done
reconnect
"$ADB" logcat -d | grep -E "VR_PERF|AndroidRuntime|FATAL" > "$PERF" || true
echo "samples: $CSV"
echo "perf lines: $PERF ($(grep -c VR_PERF "$PERF" || true) VR_PERF entries)"
