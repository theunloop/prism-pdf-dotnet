#!/usr/bin/env bash
#
# Runs once, after the container is created. Everything here is idempotent, and nothing here is
# allowed to fail the container build: a half-provisioned container you can open and fix is more
# useful than one that refuses to start.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
REPO_ROOT="$PWD"

echo "==> restoring and building the managed projects"
dotnet restore && dotnet build --configuration Release

# Opt-in, not imposed: a contributor's container should not acquire someone else's tooling by
# default. Set PRISMPDF_DEVCONTAINER_AGENTS=1 in devcontainer.json's containerEnv, or just run the
# npm install below by hand.
if [[ "${PRISMPDF_DEVCONTAINER_AGENTS:-0}" == "1" ]]; then
  echo
  echo "==> installing the agent CLIs"
  npm install -g @anthropic-ai/claude-code @openai/codex \
    || echo "warning: the agent CLIs did not install."
fi

echo
# The engine is downloaded, not built: the core publishes a library per platform on every tag. That
# needs an authenticated gh, which a fresh container does not have, so this is best-effort — the
# repository is perfectly usable without it, and the message says exactly what to run.
if gh auth status >/dev/null 2>&1; then
  echo "==> fetching the published native libraries and corpus"
  if build/fetch-natives.sh --corpus; then
    echo "==> engine staged; the full conformance suite will run"
  else
    echo "warning: the fetch failed. Re-run build/fetch-natives.sh --corpus to see why."
  fi
else
  cat <<'MSG'
note: gh is not authenticated, so the engine was not fetched. The managed fixtures
      (NativeSurfaceTests, CompatTests, LoaderTests) run either way; the suites that call
      into the engine will skip. To get them running:

          gh auth login
          build/fetch-natives.sh --corpus

      That needs an account with read access to the core repository, which is private.
      No Rust toolchain is involved — build/build-native.sh is only for engine work.
MSG
fi

cat <<'MSG'

==> ready

    dotnet test                                              # the full suite
    dotnet test --filter "FullyQualifiedName~CompatTests"     # no native library needed
    python3 build/gen_native_methods.py --check               # the raw layer is not stale
    build/fetch-natives.sh --corpus                           # re-fetch the engine

MSG
