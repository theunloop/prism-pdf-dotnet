#!/usr/bin/env bash
#
# Fetch the published Prism PDF native libraries for the pinned core release and stage them in the
# layout the NuGet package ships.
#
# The core publishes one prebuilt library per platform on every v* tag, so this repository does not
# cross-compile Rust — it downloads, verifies, and packages. build/build-native.sh remains the
# from-source path for anyone working on the engine itself; this is the way to not have to.
#
#   build/fetch-natives.sh                     # the tag pinned in Directory.Build.props
#   build/fetch-natives.sh --tag v0.5.0        # a different release
#   build/fetch-natives.sh --corpus            # also fetch the shared test inputs
#   build/fetch-natives.sh --all-rids          # include the RIDs this package does not ship
#
# Requires the GitHub CLI, authenticated: the core repository is private.
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CORE_REPO="theunloop/prism-pdf"
STAGE_DIR="${REPO_ROOT}/native/runtimes"
CORPUS_DIR="${REPO_ROOT}/native/corpus"
SIGNER_DIR="${REPO_ROOT}/native/test-signer"
TAG=""
WANT_CORPUS=0
ALL_RIDS=0

# The RIDs this package ships. The core also builds android-arm, android-arm64 and android-x64,
# which are deliberately excluded: a netstandard2.0 assembly cannot reach them — .NET for Android
# needs a net8.0-android target — so packaging them would add 12 MB that nothing can load. Add
# them here at the same time as that target framework, not before.
PACKAGED_RIDS=(
  win-x64 win-x86 win-arm64
  linux-x64 linux-arm64 linux-arm
  linux-musl-x64 linux-musl-arm64
  osx-x64 osx-arm64
)

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)      TAG="$2"; shift 2 ;;
    --dest)     STAGE_DIR="$2"; shift 2 ;;
    --corpus)   WANT_CORPUS=1; shift ;;
    --all-rids) ALL_RIDS=1; shift ;;
    -h|--help)
      sed -n '2,16p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

# --- Preconditions --------------------------------------------------------------------------

if ! command -v gh >/dev/null 2>&1; then
  cat >&2 <<'MSG'
error: gh not found.

The core repository is private, so its release assets need an authenticated request. Install the
GitHub CLI (https://cli.github.com) and run `gh auth login`.
MSG
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "error: gh is not authenticated. Run: gh auth login" >&2
  exit 1
fi

# One source of truth for the pinned release: the property the build already reads.
if [[ -z "${TAG}" ]]; then
  TAG="$(sed -n 's/.*<PrismPdfCoreTag>\(.*\)<\/PrismPdfCoreTag>.*/\1/p' \
    "${REPO_ROOT}/Directory.Build.props")"
fi
if [[ -z "${TAG}" ]]; then
  echo "error: no <PrismPdfCoreTag> in Directory.Build.props, and no --tag given" >&2
  exit 1
fi
VERSION="${TAG#v}"

echo "==> Prism PDF ${TAG} from ${CORE_REPO}"

WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

# --- Download and verify --------------------------------------------------------------------

BUNDLE="prism-pdf-natives-${TAG}.tar.gz"
SUMS="SHA256SUMS-${TAG}.txt"
PATTERNS=(--pattern "${BUNDLE}" --pattern "${SUMS}")
if [[ ${WANT_CORPUS} -eq 1 ]]; then
  PATTERNS+=(--pattern "prism-pdf-corpus-${TAG}.tar.gz")
fi

echo "==> downloading"
gh release download "${TAG}" --repo "${CORE_REPO}" --dir "${WORK}" "${PATTERNS[@]}"

# Two layers of checksums, and both are worth spending. This one covers the published files and
# is what detects a truncated or substituted download.
echo "==> verifying the published files"
( cd "${WORK}" && grep -F -f <(ls) "${SUMS}" | sha256sum -c - )

echo "==> unpacking"
tar xzf "${WORK}/${BUNDLE}" -C "${WORK}"
ROOT="${WORK}/prism-pdf-natives-${TAG}"

# ...and this one covers every file inside the bundle, which is what a binding records per RID.
echo "==> verifying the bundle contents"
( cd "${ROOT}" && sha256sum -c SHA256SUMS >/dev/null && echo "    all files OK" )

if [[ "$(cat "${ROOT}/VERSION")" != "${VERSION}" ]]; then
  echo "error: bundle reports version $(cat "${ROOT}/VERSION"), expected ${VERSION}" >&2
  exit 1
fi

# The header is the contract the raw P/Invoke layer was generated from. If the binaries were built
# from a different one, every signature in NativeMethods.cs is a guess — so this is a hard error,
# not the advisory note build-native.sh prints for a source checkout.
if ! diff -q "${ROOT}/prismpdf.h" "${REPO_ROOT}/native/include/prismpdf.h" >/dev/null; then
  cat >&2 <<MSG
error: the vendored header is not the one these binaries were built from.

    vendored: native/include/prismpdf.h
    bundled:  prism-pdf-natives-${TAG}.tar.gz

Re-vendor it and regenerate the raw layer before packaging (native/VENDORED.md):

    gh release download ${TAG} --repo ${CORE_REPO} --pattern 'prism-pdf-${TAG}.h'
    cp prism-pdf-${TAG}.h native/include/prismpdf.h
    python3 build/gen_native_methods.py
MSG
  exit 1
fi
echo "==> header matches the vendored copy"

# --- Stage ------------------------------------------------------------------------------------

if [[ ${ALL_RIDS} -eq 1 ]]; then
  mapfile -t RIDS < <(cd "${ROOT}" && find . -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort)
else
  RIDS=("${PACKAGED_RIDS[@]}")
fi

rm -rf "${STAGE_DIR}"
echo "==> staging ${#RIDS[@]} runtime identifiers into ${STAGE_DIR#"${REPO_ROOT}"/}"
for rid in "${RIDS[@]}"; do
  if [[ ! -d "${ROOT}/${rid}" ]]; then
    echo "error: the bundle has no ${rid}; the core's matrix may have changed" >&2
    exit 1
  fi
  mkdir -p "${STAGE_DIR}/${rid}/native"
  cp "${ROOT}/${rid}"/* "${STAGE_DIR}/${rid}/native/"
done

if [[ ${WANT_CORPUS} -eq 1 ]]; then
  rm -rf "${CORPUS_DIR}"
  mkdir -p "${CORPUS_DIR}"
  tar xzf "${WORK}/prism-pdf-corpus-${TAG}.tar.gz" -C "${WORK}"
  cp -R "${WORK}/corpus/." "${CORPUS_DIR}/"
  echo "==> staged the corpus into ${CORPUS_DIR#"${REPO_ROOT}"/}"

  # The one test input the release does not package. The signing journey needs the core's
  # throwaway signing key, which lives only in the source tree — so read those three files
  # straight out of the tag through the contents API rather than making every developer and
  # every CI leg clone a 400 MB repository for 2 KB. They are a test fixture, not a credential:
  # the core publishes them in a public-facing example directory.
  rm -rf "${SIGNER_DIR}"
  mkdir -p "${SIGNER_DIR}"
  for file in cert.der key.der README.md; do
    if ! gh api "repos/${CORE_REPO}/contents/crates/pdf/examples/test-signer/${file}?ref=${TAG}" \
         -H "Accept: application/vnd.github.raw" > "${SIGNER_DIR}/${file}"; then
      echo "error: could not fetch the test signer's ${file} at ${TAG}" >&2
      exit 1
    fi
  done
  echo "==> staged the test signer into ${SIGNER_DIR#"${REPO_ROOT}"/}"
fi

# --- Report -----------------------------------------------------------------------------------
#
# The per-RID checksums belong in native/VENDORED.md, so print them in the shape that file wants
# rather than leaving the reader to compute them.

echo
echo "Commit: $(cat "${ROOT}/COMMIT")"
echo
echo "| RID | Library | SHA-256 |"
echo "|---|---|---|"
for rid in "${RIDS[@]}"; do
  lib="$(ls "${STAGE_DIR}/${rid}/native")"
  sum="$(sha256sum "${STAGE_DIR}/${rid}/native/${lib}" | cut -d' ' -f1)"
  echo "| \`${rid}\` | \`${lib}\` | \`${sum}\` |"
done
echo
echo "Total: $(du -sh "${STAGE_DIR}" | cut -f1) staged. \`dotnet pack\` will carry these."

if [[ ${WANT_CORPUS} -eq 1 ]]; then
  echo
  echo "Test inputs:"
  echo "| File | SHA-256 |"
  echo "|---|---|"
  for file in cert.der key.der; do
    echo "| \`test-signer/${file}\` | \`$(sha256sum "${SIGNER_DIR}/${file}" | cut -d' ' -f1)\` |"
  done
fi
