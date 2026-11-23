#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
rid="${1:-linux-x64}"
include="${2:-}"

if [[ "$rid" == all ]]; then
    rids=(linux-x64 win-x64 osx-arm64 osx-x64)
else
    rids=("$rid")
fi

for current in "${rids[@]}"; do
    case "$current" in
        linux-x64|win-x64|osx-arm64|osx-x64) ;;
        *)
            echo "RID non supportato: $current" >&2
            exit 2
            ;;
    esac

    output="$PWD/artifacts/$current"

    for project in Desktop Cli; do
        dotnet publish "src/ShadowHeartsItalianPatcher.$project" \
            -c Release \
            -r "$current" \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:PublishTrimmed=false \
            -p:DebugType=None \
            -p:DebugSymbols=false \
            -o "$output"
    done

    mkdir -p "$output/Patches"
    cp README.md PATCHING_ARCHITECTURE.md THIRD_PARTY_NOTICES.md LICENSE "$output/"
    cp -R licenses docs "$output/"

    if [[ "$include" == --with-local-patches ]]; then
        cp -R Patches/. "$output/Patches/"
    else
        cp Patches/README.md "$output/Patches/"
    fi
done
