#!/usr/bin/env bash

# Downloads sample files from libopx GitHub releases.
# Fetches input.* test files (bin, mxf, t42, ts, vbi, vbid) from the specified
# GitHub release and saves them to ../samples relative to this script.

set -e

VERSION="${1:-v1.0.0}"

# Determine sample directory relative to script location
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SAMPLES_DIR="$(cd "$SCRIPT_DIR/.." && pwd)/samples"

# Create samples directory if it doesn't exist
if [ ! -d "$SAMPLES_DIR" ]; then
    mkdir -p "$SAMPLES_DIR"
    echo "Created directory: $SAMPLES_DIR"
fi

# Define files to download
BASE_URL="https://github.com/nathanpbutler/libopx/releases/download/$VERSION"
FILES=(
    "input.bin"
    "input.mxf"
    "input.t42"
    "input.ts"
    "input.vbi"
    "input.vbid"
)

echo -e "\033[0;36mDownloading sample files from release $VERSION...\033[0m"

for FILE in "${FILES[@]}"; do
    URL="$BASE_URL/$FILE"
    OUTPUT_PATH="$SAMPLES_DIR/$FILE"

    echo -n "  Downloading $FILE..."
    if curl -L -f -s -o "$OUTPUT_PATH" "$URL"; then
        echo -e " \033[0;32mOK\033[0m"
    else
        echo -e " \033[0;31mFAILED\033[0m"
        echo "Warning: Failed to download $FILE" >&2
    fi
done

echo -e "\n\033[0;32mDownload complete. Files saved to: $SAMPLES_DIR\033[0m"
