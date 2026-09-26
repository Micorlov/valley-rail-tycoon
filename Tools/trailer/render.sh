#!/usr/bin/env bash
# Runs one TrailerRenderTests test in the Unity editor (PlayMode, GPU, no -nographics) once no other editor holds the project.
#   Tools/trailer/render.sh SurveysTheWorld
#   Tools/trailer/render.sh RendersTheTrailerShots
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity}"
TEST="${1:?test name}"
mkdir -p Logs
for attempt in 1 2 3 4 5; do
  while [ "$(ps -axo comm | grep -c -E 'MacOS/Unity$')" != 0 ]; do sleep 5; done
  "$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
    -testFilter "ValleyRail.Tests.TrailerRenderTests.$TEST" \
    -testResults "$PWD/Logs/trailer-$TEST.xml" -logFile "$PWD/Logs/trailer-$TEST.log" > "Logs/trailer-$TEST.stdout" 2>&1
  code=$?
  # A lost race for the project lock is only reported on stdout.
  if grep -q "another Unity instance is running" "Logs/trailer-$TEST.stdout"; then
    sleep 10
    continue
  fi
  grep -o 'result="[A-Za-z]*"' "Logs/trailer-$TEST.xml" 2>/dev/null | head -3
  exit $code
done
echo "gave up: the project stayed locked"
exit 1
