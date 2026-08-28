# Handoff: publishing versioned native artifacts

**To:** the Prism PDF core team
**From:** the .NET binding (`prism-pdf-dotnet`)
**Status:** **delivered and implemented in core `v0.4.0`.** Kept as the record of what was asked
for, and why.

> Superseded by the core's own [`docs/native-artifacts.md`](https://github.com/theunloop/prism-pdf/blob/v0.4.0/docs/native-artifacts.md),
> which is the contract now. `v0.4.0` implements everything below and goes further: 13
> shared-library RIDs rather than nine (adding `linux-arm` and three Android ABIs), the Apple
> platforms as static slices in a `PrismPDF.xcframework`, a committed glibc floor of 2.17, and a
> release-workflow guard that fails a tag whose workspace version disagrees. All five open
> questions at the end were answered. Names in this document have been carried through the
> rebrand for consistency, but it was written when the engine was still called LimoPDF.

## The ask, in one paragraph

Every binding currently clones `prism-pdf` at a tag and compiles `pdf-ffi` itself. That works for
development and does not work for shipping: a NuGet package has to carry **nine** prebuilt native
libraries, one per platform, and no binding repository should be in the business of cross-compiling
Rust. We would like the core to publish, once per release tag, a set of versioned artifacts that
every binding consumes by version and checksum instead of by source checkout.

## What the binding uses the core repository for today

Three things, and only three — this is the whole dependency surface:

| What | Where it is used | Already vendored? |
|---|---|---|
| `crates/pdf-ffi/include/prismpdf.h` | `native/include/prismpdf.h`, and `build/gen_native_methods.py` generates the raw P/Invoke layer from it | ✅ committed, cbindgen output, never hand-edited |
| `pdf_ffi` cdylib | `build/build-native.sh` compiles and stages it into `native/lib/` | ❌ built locally, gitignored |
| `corpus/{valid,malformed,edge}` | `tests/PrismPdf.Tests/Corpus.cs` — the shared conformance inputs | ❌ read straight out of the checkout, deliberately not forked |

The header pattern is the one that already works, and it is the model for the rest: **copy text
contracts, build binaries**. What is missing is a way to get the binaries and the corpus without a
source checkout.

## What we are asking you to publish

Per release tag, one bundle containing:

1. **The native libraries**, one per RID — the matrix below.
2. **`prismpdf.h`**, the exact header those binaries were built from, so a binding can verify the
   header it vendored matches the binaries it downloaded.
3. **`corpus.tar.gz`** — `valid/`, `malformed/`, `edge/`. Small, and it is what makes the "same
   journeys, same inputs, same assertions" rule in `docs/BINDINGS.md` enforceable across bindings.
4. **`SHA256SUMS`**, plus the release's **commit SHA** recorded in plain text.

## The target matrix

| NuGet RID | Rust target triple | Artifact |
|---|---|---|
| `win-x64` | `x86_64-pc-windows-msvc` | `pdf_ffi.dll` |
| `win-x86` | `i686-pc-windows-msvc` | `pdf_ffi.dll` |
| `win-arm64` | `aarch64-pc-windows-msvc` | `pdf_ffi.dll` |
| `linux-x64` | `x86_64-unknown-linux-gnu` | `libpdf_ffi.so` |
| `linux-arm64` | `aarch64-unknown-linux-gnu` | `libpdf_ffi.so` |
| `linux-musl-x64` | `x86_64-unknown-linux-musl` | `libpdf_ffi.so` |
| `linux-musl-arm64` | `aarch64-unknown-linux-musl` | `libpdf_ffi.so` |
| `osx-x64` | `x86_64-apple-darwin` | `libpdf_ffi.dylib` |
| `osx-arm64` | `aarch64-apple-darwin` | `libpdf_ffi.dylib` |

Optional, if 32-bit Linux ARM (Raspberry Pi OS 32-bit) is in scope: `linux-arm` /
`armv7-unknown-linux-gnueabihf`.

## Four build settings that decide whether this is actually portable

These are the difference between "works on the build machine" and "works on the consumer's
machine". None of them can be fixed after the fact by the binding.

**1. Static CRT on Windows.** Rust's MSVC targets link the VC++ runtime dynamically, so consumers
without the redistributable installed get a load failure. Build the three Windows legs with
`-C target-feature=+crt-static` so the DLL is self-contained.

**2. A glibc floor on Linux.** A build on `ubuntu-latest` (24.04) links glibc 2.39 and will not load
on RHEL 8 or Ubuntu 20.04. Build the glibc legs in an old container (manylinux-style), or with
`cross`, or `cargo-zigbuild --target x86_64-unknown-linux-gnu.2.17`. Pick a floor and state it in
the release notes so bindings can document it.

**3. musl is a separate build, not a copy.** A glibc `.so` cannot load on Alpine. (Related: musl has
no `libdl` — it implements `dlopen` inside libc — which the .NET loader now handles, but only
because the musl binary exists to be loaded.)

**4. macOS deployment target and signing.** Set an explicit minimum deployment target, and ad-hoc
codesign the dylibs in CI. A dylib downloaded from CI otherwise trips Gatekeeper quarantine and the
consumer sees a load failure they have to `xattr -d` their way out of.

Also worth doing while you are in there: the workspace has no `[profile.release]` section, so the
binaries ship with symbols. `strip = "symbols"` would cut size before any of this multiplies by
nine. For reference, the current Linux ARM64 build is **6.58 MB raw, 2.34 MB gzipped**.

## The version contract

**`prismpdf_version()` must match the release tag.** Today it does not: `v0.3.1` was cut without
bumping the Cargo workspace version, so the ABI reports `0.3.0`. Every binding's vertical slice
asserts on that string — ours does, and `native/VENDORED.md` carries a paragraph explaining the
discrepancy. Please bump the workspace version in the release commit going forward, or state the
expected value in the release notes for each tag.

Two more properties we are relying on and would like stated explicitly per release:

- **The ABI stays append-only** (`docs/ABI.md`, "Versioning policy"). This is what lets a binding
  built against an older header keep working against newer binaries — and it is why a managed-only
  fix can ship without rebuilding any Rust.
- **Artifacts are immutable.** Never re-upload a version with different bytes. Bindings pin by
  SHA-256, and a silent replacement turns into an unreproducible build days later.

## Where the artifacts should live

The core is private (`https://github.com/theunloop/prism-pdf.git`), so wherever they land has
to be reachable by binding CI with a token. Roughly in order of how little work they are:

| Option | Notes |
|---|---|
| Bitbucket Downloads on the core repo | Simplest; an app password in binding CI is enough |
| An internal NuGet feed as `PrismPdf.NativeAssets` | Best fit for the .NET binding specifically — versioned, cached, restore handles it. Other bindings would need a different channel |
| S3 / Artifactory / GitHub Packages | Neutral across bindings; more setup |

If you want one channel that serves every binding equally, a plain HTTPS artifact store beats a
language-specific package feed. The .NET side can wrap whatever you choose.

## What the binding does with them

So you know what the other end of the contract looks like:

- `build/fetch-natives.sh` downloads the bundle for the pinned version, verifies every SHA-256, and
  stages `runtimes/<rid>/native/`. `native/VENDORED.md` gains a checksum per RID next to the tag and
  commit it already records.
- CI packs **one** NuGet carrying all nine RIDs — roughly 21 MB, which is unremarkable for a native
  package.
- `build/build-native.sh` stays exactly as it is, as the from-source path for anyone working on the
  engine itself. This is not a replacement for building from source; it is a way to not have to.
- **Consumers never download anything at runtime.** The binaries are inside the package. A
  fetch-on-first-use design would break offline and air-gapped consumers and would be a
  supply-chain liability. Worth saying out loud because it is a tempting shortcut.

## Sequencing

Each phase is independently useful, and the first two need nothing from the .NET side:

1. **Version sync + `strip = "symbols"`.** Small, and removes the `0.3.0` / `v0.3.1` footgun every
   binding currently documents around.
2. **The natives matrix** with the four settings above. This is the phase that unblocks shipping.
3. **The corpus tarball.** Unblocks removing the source checkout from binding CI entirely.
4. **The .NET binding switches over** — fetch by version, drop the checkout from CI and the
   devcontainer, keep the from-source path for engine work.

## Open questions for you

1. **Is mobile in scope?** iOS forbids loading a separately shipped dylib — native code must be
   statically linked and declared `DllImport("__Internal")`. If Xamarin/Unity/iOS reach is a real
   goal, we need `libpdf_ffi.a` for the Apple targets too, and the binding needs multi-targeting.
   If it is not, several bindings should narrow their advertised reach.
2. **Is `linux-arm` (32-bit ARMv7) in scope?**
3. **Who owns the release pipeline** — does core CI grow the matrix, or does a binding repo build
   natives for everyone? Core is the better home: it already has the source, and one pipeline serves
   every binding.
4. **Corpus in the same bundle, or its own artifact?** Its own is easier to version independently,
   since it changes less often than the engine.
5. **What glibc floor do you want to commit to?** This decides which distros the whole ecosystem
   supports, so it is your call, not a binding's.
