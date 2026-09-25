#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p Logs
dotnet run --project Tests/CoreChecks.csproj | tee Logs/core-checks.txt
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/Logs/editmode-final.xml" -logFile "$PWD/Logs/editmode-final.log"
"$UNITY_EDITOR" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults "$PWD/Logs/playmode-final.xml" -logFile "$PWD/Logs/playmode-final.log"
if [[ "${1:-}" == "--android" ]]; then
  "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Android -executeMethod ValleyRail.Editor.ProjectSetup.BuildAndroid -logFile "$PWD/Logs/android-final.log"
fi
