#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ODIN_ROOT="${ODIN_ROOT:-/home/abotero/abotero/odin}"

echo "Building Odin2Cs..."
"$ODIN_ROOT/odin" build "$SCRIPT_DIR" -out:"$SCRIPT_DIR/odin2cs" -o:size

echo "Build complete: $SCRIPT_DIR/odin2cs"
