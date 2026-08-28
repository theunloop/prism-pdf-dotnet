#!/usr/bin/env bash
#
# Build the Prism PDF native library (pdf_ffi) from a prism-pdf checkout and stage it where the
# SDK's loader will find it.
#
# The binding consumes the C ABI, not the Rust crates, so this is the only step that needs a Rust
# toolchain. CI builds this once per OS and publishes the result; day to day you run it after
# cloning prism-pdf beside this repo.
#
#   build/build-native.sh                      # uses ./prism-pdf, release profile
#   build/build-native.sh --core ../prism-pdf --debug
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CORE_DIR="${REPO_ROOT}/prism-pdf"
PROFILE="release"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --core)  CORE_DIR="$2"; shift 2 ;;
    --debug) PROFILE="debug"; shift ;;
    -h|--help)
      sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

# --- Preconditions --------------------------------------------------------------------------

if ! command -v cargo >/dev/null 2>&1; then
  cat >&2 <<'MSG'
error: cargo not found.

Prism PDF's core is a Rust workspace, so building the native library needs a Rust toolchain
(1.88+, edition 2024). Install one with:

    curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

then re-run this script. See docs/native-build.md.
MSG
  exit 1
fi

if [[ ! -f "${CORE_DIR}/crates/pdf-ffi/Cargo.toml" ]]; then
  cat >&2 <<MSG
error: no prism-pdf checkout at ${CORE_DIR}

Clone it (at the tag this binding is pinned to — see native/VENDORED.md):

    git clone --branch v0.4.0 git@github.com:theunloop/prism-pdf.git "${CORE_DIR}"

or point this script elsewhere with --core <path>.
MSG
  exit 1
fi

# --- Build ----------------------------------------------------------------------------------

echo "==> building pdf_ffi (${PROFILE}) from ${CORE_DIR}"
if [[ "${PROFILE}" == "release" ]]; then
  ( cd "${CORE_DIR}" && cargo build -p pdf-ffi --release )
else
  ( cd "${CORE_DIR}" && cargo build -p pdf-ffi )
fi

# --- Stage ----------------------------------------------------------------------------------

case "$(uname -s)" in
  Darwin)  LIB_NAME="libpdf_ffi.dylib" ;;
  Linux)   LIB_NAME="libpdf_ffi.so" ;;
  MINGW*|MSYS*|CYGWIN*) LIB_NAME="pdf_ffi.dll" ;;
  *) echo "unsupported platform: $(uname -s)" >&2; exit 1 ;;
esac

BUILT="${CORE_DIR}/target/${PROFILE}/${LIB_NAME}"
if [[ ! -f "${BUILT}" ]]; then
  echo "error: cargo reported success but ${BUILT} is missing" >&2
  exit 1
fi

STAGE_DIR="${REPO_ROOT}/native/lib"
mkdir -p "${STAGE_DIR}"
cp "${BUILT}" "${STAGE_DIR}/"

# --- Header drift check ---------------------------------------------------------------------
#
# The ABI is append-only, so an older vendored header still links against a newer library. A
# *difference* is therefore not an error — but it does mean there is new surface to consider
# binding, so say so rather than staying quiet.

VENDORED="${REPO_ROOT}/native/include/prismpdf.h"
UPSTREAM="${CORE_DIR}/crates/pdf-ffi/include/prismpdf.h"
if ! diff -q "${VENDORED}" "${UPSTREAM}" >/dev/null 2>&1; then
  ADDED=$(grep -c '^> .*prismpdf_' <(diff "${VENDORED}" "${UPSTREAM}") || true)
  echo "note: the vendored header differs from ${CORE_DIR} (${ADDED} added export line(s))."
  echo "      Re-vendor with: cp '${UPSTREAM}' '${VENDORED}'"
  echo "      then: python3 build/gen_native_methods.py && update native/VENDORED.md"
fi

echo "==> staged ${STAGE_DIR}/${LIB_NAME}"
echo
echo "Point the SDK at it with:"
echo "    export PRISMPDF_NATIVE_PATH='${STAGE_DIR}'"
