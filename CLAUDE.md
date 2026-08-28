# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

The **.NET binding** for Prism PDF, a pure-Rust PDF engine. It is a binding, not a reimplementation:
every capability comes from the engine's stable C ABI (`pdf-ffi`) across a handle-based boundary.
This repo contributes a generated raw P/Invoke layer and a hand-written idiomatic layer.

The engine lives in a separate repo, `git@github.com:theunloop/prism-pdf.git`. **It is not checked
out here.** Everything this repository needs from it — the native libraries, the header, the shared
corpus, the signing fixture — comes from its published release artifacts via
`build/fetch-natives.sh`. A checkout is only for working on the engine itself, and can live
anywhere (`build/build-native.sh --core <path>`).

The authorities this SDK is written against live in that core repo and **win over this repo's docs
when they disagree**: `docs/BINDINGS.md` (the binding author's guide — naming rules, the six
semantic contracts, the conformance journeys), `crates/pdf-ffi/include/prismpdf.h` (each function's
exact ownership contract), `docs/ABI.md`, and `docs/site/ffi-ownership.md`.

## Commands

```bash
build/fetch-natives.sh --corpus                           # do this first: natives + corpus, verified
dotnet build                                              # solution is PrismPdf.slnx
dotnet test                                               # full conformance suite
dotnet test --filter "FullyQualifiedName~VerticalSliceTests"
dotnet test --filter "Name=Save_RoundTripsThroughTheBinding"   # a single test

python3 build/gen_native_methods.py                       # regenerate the raw layer
python3 build/gen_native_methods.py --check               # fail if stale (CI gate)

dotnet pack src/PrismPdf/PrismPdf.csproj -c Release       # ~20 MB, ten RIDs inside
build/smoke-package.sh --rid linux-arm64                  # install the .nupkg and actually run it

build/build-native.sh                                     # from source instead: stages native/lib/
build/build-native.sh --core ../prism-pdf --debug
export PRISMPDF_NATIVE_PATH="$PWD/native/lib"              # only needed for the from-source path
```

Requires a **.NET SDK 9.0.200+** (the `.slnx` solution format; an 8.0.x SDK fails with `MSB1003`),
the **.NET 8 runtime** (the test project targets `net8.0`; the library targets `netstandard2.0`),
an authenticated **`gh`** for `build/fetch-natives.sh` (the core repository is private), and
**Rust 1.88+** only for `build/build-native.sh`.

`TreatWarningsAsErrors` is on repo-wide, and so is `EnforceCodeStyleInBuild` — a style violation
from `.editorconfig` fails the build.

### Running without the engine

`NativeSurfaceTests` (header parity) and `CompatTests` (the netstandard2.0 substitutes) are pure
managed code and always run. Everything else derives from `NativeTestBase` (in
`tests/PrismPdf.Tests/Corpus.cs`) and **skips with a message** — never fails — when `pdf_ffi` cannot
be loaded or when no corpus checkout is found. A skipped native suite is expected, not a break.

## Architecture

### Two layers, and only two

1. **Raw layer** — `src/PrismPdf/Interop/NativeMethods.cs`, `internal`, a flat 1:1 projection of the
   header. No logic, no ownership, no error handling. `NativeLibraryResolver` sits beside it.
2. **Idiomatic layer** — everything else under `src/PrismPdf/`. The public API, and the *only* place
   the ABI's ownership and error conventions are enforced.

### `NativeMethods.cs` is generated — never hand-edit it

`build/gen_native_methods.py` parses `native/include/prismpdf.h` and emits one `[DllImport]` per
export. Adding a new area means adding its export names to the `AREAS` list in that script and
regenerating, then writing the idiomatic layer for them. Three projections the generator makes:
C `bool` → `byte`, every opaque handle → `nint`, `char *`/`uint8_t *` → `byte*`. Every declaration
pins `CallingConvention.Cdecl` and `ExactSpelling`; `NativeSurfaceTests` enforces both.

### Where each contract is enforced — change these and read `FailurePathTests`

| Contract | Enforced in |
|---|---|
| One exception type carrying the stable status | `PrismPdfException.Create` |
| `NotFound` on an optional getter is *absence* (`null`), on an index it is an error | `Native.CheckOptional` |
| Consuming calls invalidate on success only | `PrismPdfHandle.MarkConsumed` |
| Borrowed items keep their owning list alive, and raise once it is disposed | `BorrowedItem` (`Collections/NativeList.cs`) |
| Owned strings/buffers are copied then freed immediately | `Native.TakeString`, `Native.TakeBytes` |
| No shared mutable handles across threads | documented only, not enforced |

Two consequences that bite when writing new bindings: the library's diagnostic slot is
**thread-local and cleared by the next successful call**, so never put an `await` or thread hop
between a native call and reading its error; and `Pdf.Version` is the one string the library owns
forever and must not be freed.

### Design rules that look like omissions but are not

- **There is no `Page` type.** The ABI has no page handle — page-indexed calls take an index on the
  document (`doc.PageText(0)`). A `Page` façade would diverge from every other Prism PDF binding.
- **The boundary is immutable.** Every transform returns *new* bytes and leaves its input untouched;
  there is no mutating `doc.Save()`. Tests assert by reopening the returned bytes.
- **`…WithReport` companions, not optional parameters** (`Save()` / `SaveWithReport()`).
- **Flattened ABI enums stay flattened** (e.g. `builder_add_link_uri` vs `_add_link_page`) even
  though C# could express them as a discriminated shape.
- Naming follows the guide's ten rules mechanically; the three forced deviations (statics on `Pdf`
  not `PrismPdf`, `PrismPdfStatus` keeps its prefix, `PdfDate`/`OpenOptions` reshape C structs) are
  recorded in `docs/naming.md`. Record any new deviation there.

### netstandard2.0 is load-bearing

It is why the code uses `[DllImport]` rather than `[LibraryImport]`, why `src/PrismPdf/Compat/`
re-declares `NativeMemory`, `IsExternalInit`, `ModuleInitializer` and the argument guards as
`internal`, and why `NativeLibraryResolver` looks `NativeLibrary.SetDllImportResolver` up by
reflection (falling back to pre-loading via `LoadLibraryW`/`dlopen`). Do not reach for a newer BCL
API without adding a `Compat/` substitute and a `CompatTests` case.

### The native payload is downloaded, not built

The core publishes one prebuilt `pdf_ffi` per platform on every `v*` tag, so this repository never
cross-compiles Rust. `build/fetch-natives.sh` downloads the bundle for `PrismPdfCoreTag`, verifies
two layers of checksums, refuses to continue if the bundled header is not the vendored one, and
stages `native/runtimes/<rid>/native/`. That directory is three things at once: what `dotnet pack`
carries into `runtimes/`, what the test project copies beside its assembly, and therefore what the
suite actually runs against.

**Ten RIDs, not thirteen.** The core also builds `android-{arm,arm64,x64}`; a netstandard2.0
assembly cannot load them, so they are excluded until there is a `net8.0-android` target. The list
lives in `PACKAGED_RIDS` in the fetch script. Apple's static slices need `DllImport("__Internal")`
and multi-targeting — a separate milestone, not a packaging flag.

**One package, not one per platform.** NuGet downloads a package whole either way, so splitting
would save a consumer nothing and cost them a RID in their `PackageReference`. `build/PrismPdf.targets`
covers the hosts that have no `deps.json` to read (.NET Framework, some Mono) by copying the
Windows RIDs to the output directory, where the resolver's own probing finds them.

`build/smoke-package.sh` is the only test of any of this: everything else reaches the library
through a ProjectReference, which never exercises NuGet's RID asset selection.

## Updating to a newer core release

The ABI is **append-only**, so an older vendored header keeps working against a newer library; a
header diff is new surface to consider, not a break. The sequence (also in `native/VENDORED.md`):

1. `gh release download <tag> --repo theunloop/prism-pdf --pattern 'prism-pdf-<tag>.h'`, verify it
   against `SHA256SUMS-<tag>.txt`, and copy it over `native/include/prismpdf.h` (never hand-edit —
   it is cbindgen output).
2. `python3 build/gen_native_methods.py` and review the diff.
3. Update the tables in `native/VENDORED.md`, `PrismPdfCoreTag` in `Directory.Build.props`, and
   `PRISMPDF_CORE_TAG` in `.github/workflows/ci.yml` — all three must stay in sync.
4. `build/fetch-natives.sh --corpus`, which re-checks the header against the new binaries and
   prints the per-RID checksum table for `native/VENDORED.md`.
5. `dotnet test`, then `build/smoke-package.sh`; note newly bound surface in `CHANGELOG.md`.

**The tag is the version.** `prismpdf_version()` equals the release tag without its `v`, enforced
by a guard in the core's release workflow, so the vertical slice asserts on it directly. Core
`v0.3.1` and earlier predate that guard and reported a trailing workspace version.

## Tests

The suite is a port of the core's conformance journeys, run against the core repo's shared
`corpus/{valid,malformed,edge}` — located via `PRISMPDF_CORPUS` or a checkout above the test output
directory. **Do not fork corpus files into this repo**; the point of the shared corpus is that the
bindings cannot drift.

One semantic worth knowing before writing corpus assertions: `OpenMode.Recovered` means *the
cross-reference was rebuilt* (`XrefParseFailure` / `UnreachableCatalog`), not *the parser was
lenient*. Two files in `malformed/` open in `Strict` mode for that reason — see the note in
`docs/conformance-suite.md`.

`VerticalSliceTests`, `ParseJourneyTests`, `ManipulateJourneyTests`, `SecurityJourneyTests`,
`FailurePathTests` are the ported journeys; `NativeSurfaceTests` is the raw-layer completeness check
and *prints* the coverage gap against the header (unbound exports are a roadmap item, not a
failure), so it is more authoritative than `docs/roadmap.md`. The create and compose journeys are
blocked until `Builder` and `Composition` are bound.
