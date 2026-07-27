#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ODIN_INTEROP_SOURCE="${ODIN_INTEROP_SOURCE:-$SCRIPT_DIR/../../../../../Assets/Odin}"
OUTPUT_DIR="${OUTPUT_DIR:-$SCRIPT_DIR/../../../../../Assets/Odin/Generated}"

# Resolve to absolute paths
ODIN_INTEROP_SOURCE="$(cd "$ODIN_INTEROP_SOURCE" && pwd)"
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

"$SCRIPT_DIR/odin2cs" "$ODIN_INTEROP_SOURCE" "$OUTPUT_DIR"
