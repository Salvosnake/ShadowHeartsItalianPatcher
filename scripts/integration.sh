#!/usr/bin/env bash
set -euo pipefail
# ISO proprietarie lette da riferimenti locali, mai aggiunte al repository.
# SOURCE_ISO=/path/original.iso REFERENCE_ISO=/path/reference.iso PACKAGE_DIR=/path/Patches OUTPUT_DIR=/new/dir ./scripts/integration.sh
cd "$(dirname "$0")/.."
: "${SOURCE_ISO:?Imposta SOURCE_ISO}"
: "${REFERENCE_ISO:?Imposta REFERENCE_ISO}"
: "${PACKAGE_DIR:?Imposta PACKAGE_DIR}"
: "${OUTPUT_DIR:?Imposta OUTPUT_DIR}"
mkdir -p "$OUTPUT_DIR"
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- apply "$SOURCE_ISO" "$OUTPUT_DIR" "$PACKAGE_DIR"
for result in "$OUTPUT_DIR"/ShadowHeartsCovenant_USA_Disc*_Italian.iso; do
    cmp -- "$REFERENCE_ISO" "$result"
    sha256sum -- "$SOURCE_ISO" "$REFERENCE_ISO" "$result"
done
