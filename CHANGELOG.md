# Changelog

All notable changes to the Prism PDF .NET SDK are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is `0.x`, the public
API may change in minor releases.

Each entry records the Prism PDF core release the binding was built against, because that — not this
SDK's own version — determines what the engine underneath can do.

## [Unreleased]

### Added

Initial binding against Prism PDF core **`v0.4.0`**.

> The engine was called **LimoPDF** until core `v0.4.0` renamed it to **Prism PDF**. Nothing had
> shipped from this repository, so the binding took the new name wholesale rather than carrying a
> compatibility shim: the namespace and package are `PrismPdf`, the vendored header is
> `prismpdf.h`, and the environment variables are `PRISMPDF_NATIVE_PATH` and `PRISMPDF_CORPUS`.
> The ABI itself did not move — the `v0.4.0` header is byte-identical to `v0.3.1`'s once the
> `limopdf_*` → `prismpdf_*` rename is applied, still 386 exports — so the rename cost the
> binding no behaviour.

- **Raw layer** — 159 `[DllImport]` declarations generated from the vendored `prismpdf.h` by
  `build/gen_native_methods.py`, with cdecl and `ExactSpelling` pinned on every one.
- **Targets `netstandard2.0`**, so the binding is consumable from .NET Framework 4.6.1+, .NET Core
  2.0+, .NET 5+, Mono, Unity and Xamarin. `src/PrismPdf/Compat/` re-declares, `internal`, what
  netstandard2.0 lacks — `NativeMemory`, the `ThrowIf*` argument guards, `IsExternalInit`,
  `ModuleInitializerAttribute`, `CallerArgumentExpressionAttribute` — and `NativeLibraryResolver`
  installs `SetDllImportResolver` reflectively when the hosting runtime has it, falling back to
  pre-loading the library through the platform loader when it does not.
- **Lifetime model** — `PrismPdfHandle` (deterministic disposal, finalizer safety net, consumed-on-
  success marking) and `BorrowedItem` (holds its owner alive, refuses use after the owner is
  disposed).
- **Errors** — one `PrismPdfException` carrying the stable `PrismPdfStatus`, with the thread-local
  diagnostic read immediately and only trusted when its status matches the failed call.
- **`Document`** — open (plain, password, `OpenOptions`, private key), read (page count, versions,
  text, positioned text, `/Info`, XMP, dates), and the immutable transform surface (save in four
  modes, extract, rotate, subset fonts, fill and flatten forms), each with a `…WithReport`
  companion.
- **Collections** — `AnnotationList`, `FormFieldList`, `OutlineList`, `AttachmentList`, `FontList`,
  `ImageList`, `SignatureList`, `StringList` and their borrowed items.
- **Security** — `Permissions`, the `SaveEncrypted…` family (RC4, AES-128, AES-256, AES-256-GCM,
  public-key, PDF MAC), `SignSettings`, signing, timestamping, and verification with integrity,
  trust and LTV.
- **`Pdf`** — `Version` and `Merge`.
- **Native library probing** — `PRISMPDF_NATIVE_PATH`, then the assembly's own directory, then the
  packaged `runtimes/<rid>/native/` layout, then `native/lib/`, then the runtime's default rules.
  The identifier comes from the *process* architecture, and on Linux carries the libc flavour, so
  a musl host asks for `linux-musl-<arch>` before `linux-<arch>`. `dlopen` is reached through
  `libdl.so.2`, `libdl` or `libc`, the last for musl systems that have no `libdl`.
- **Conformance suite** — the vertical slice, the parse / manipulate / failure-path / security
  journeys against the shared corpus, the raw-layer completeness check, `CompatTests` over the
  netstandard2.0 substitutes, and `LoaderTests` over the probing rules.
- **Packaging** — one NuGet package (~20 MB) carrying the engine for ten runtime identifiers at
  `runtimes/<rid>/native/`: `win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`,
  `linux-arm`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, `osx-arm64`. Consumers install
  nothing alongside it and nothing is fetched at run time. `build/PrismPdf.targets` ships inside
  the package for hosts with no `deps.json` — .NET Framework and some Mono configurations never
  unpack `runtimes/`, so it copies the Windows libraries to the output directory, where the
  resolver's own probing finds them. The core's Android RIDs are deliberately omitted until there
  is a `net8.0-android` target that could load them.
- **Build tooling** — `build/fetch-natives.sh` downloads the core's published natives and corpus
  for the pinned tag, verifies both the published files and every file inside the bundle, and
  refuses to continue if the bundled header is not the vendored one; `--corpus` also stages the
  shared corpus and the core's signing fixture, so the whole suite runs with **no core checkout**.
  `build/smoke-package.sh`
  packs, installs the `.nupkg` into a throwaway project from a local feed and runs a document
  through it — the only check of NuGet's RID asset selection, which a `ProjectReference` never
  exercises. `build/build-native.sh` remains the from-source path, and the raw-layer generator
  keeps its `--check` mode.

### Not yet bound

Authoring (`Builder`, content streams), layout (`Flow`), declarative composition, PDF/A and PDF/UA
production, and COS inspection — 227 of the header's 386 exports. See `docs/roadmap.md`.
