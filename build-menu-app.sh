#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
identity="${1:--}"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
app="$staging/MIIW Voice.app"
mkdir -p "$app/Contents/MacOS" dist
cp MenuApp/Info.plist "$app/Contents/Info.plist"
plutil -lint "$app/Contents/Info.plist"
clang -fobjc-arc -fblocks -Wall -Wextra -Wno-unused-parameter \
    -mmacosx-version-min=13.0 -arch arm64 -arch x86_64 MenuApp/main.m \
    -framework AppKit -framework IOKit -framework ApplicationServices -framework ServiceManagement \
    -o "$app/Contents/MacOS/MIIWVoice"
signing_options=(--timestamp=none)
if [[ "$identity" == "Developer ID Application:"* ]]; then
    signing_options=(--options runtime --timestamp)
fi
codesign --force "${signing_options[@]}" --sign "$identity" "$app"
codesign --verify --strict "$app"
ditto -c -k --keepParent "$app" "$staging/MIIW Voice.zip"
mv "$staging/MIIW Voice.zip" dist/
