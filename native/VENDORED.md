# Vendored native surface

## `include/prismpdf.h`

| | |
|---|---|
| Source | `https://github.com/theunloop/prism-pdf` (private) |
| Release asset | `prism-pdf-v0.4.0.h` |
| Tag | `v0.4.0` |
| Commit | `b1578d5f2490927720578c19a766eeae8b8c0178` |
| `prismpdf_version()` | `0.4.0` |
| SHA-256 | `308edde009557c53e88232f7328e82b1a38a35c7408e60c3f3ccbdc3e65ee24b` |
| Vendored on | 2026-08-28 |

The header is taken from the release asset rather than from a source checkout, and the checksum
above is what proves the two are the same file. The identical header also ships *inside*
`prism-pdf-natives-v0.4.0.tar.gz`, so the binaries this SDK packages can be shown to have been
built from the surface it generated its P/Invoke layer from.

`prismpdf_version()` equals the tag without its `v`. That is a guarantee, not a coincidence: the
core's release workflow refuses to build a `vX.Y.Z` tag whose workspace version disagrees. (It was
not always so — `v0.3.1` predates the guard and reported `0.3.0`, which this SDK used to document
around.)

## Why vendor at all

The binding author's guide asks every binding to copy and commit the header, recording the Prism PDF
version it came from:

> The ABI is **append-only** (`docs/ABI.md`, "Versioning policy"): existing signatures and
> status-code values never change, so a binding built against an older header keeps working
> against a newer library. New surface is found by diffing the vendored header against the new
> release's header; ABI additions are recorded in `CHANGELOG.md`.

So the vendored copy is the contract this SDK was written against. It is never edited by hand — it
is cbindgen output.

## Updating to a newer core release

1. `gh release download vX.Y.Z --repo theunloop/prism-pdf --pattern 'prism-pdf-vX.Y.Z.h' \
   --pattern 'SHA256SUMS-vX.Y.Z.txt'`, then `sha256sum -c` before trusting the file.
2. `cp prism-pdf-vX.Y.Z.h native/include/prismpdf.h`
3. `python3 build/gen_native_methods.py` — regenerates the raw layer; **review the diff**, it is
   exactly the ABI's additions restricted to the areas this SDK binds.
4. Update the table above, and `PrismPdfCoreTag` in `Directory.Build.props`.
5. `dotnet test` — `NativeSurfaceTests` re-checks the raw layer against the new header and
   reports any newly available exports the SDK does not bind yet.
6. Note anything newly bound in `CHANGELOG.md`.

## Native library

The library this header describes is **`pdf_ffi`** — `libpdf_ffi.dylib` (macOS),
`libpdf_ffi.so` (Linux), `pdf_ffi.dll` (Windows). Do not confuse it with `prismpdf`, which is the
core's CLI binary.

It is not committed. `build/fetch-natives.sh` downloads the ten libraries this package ships from
the core's release for the tag above, verifies them, and stages `native/runtimes/<rid>/native/` —
which is the layout `dotnet pack` carries and the layout the loader probes.
`build/build-native.sh` remains the from-source path for anyone working on the engine itself, and
takes `--core <path>` so that checkout can live anywhere rather than inside this repository.

### Test inputs

`--corpus` additionally stages `native/corpus/` from `prism-pdf-corpus-v0.4.0.tar.gz` and
`native/test-signer/` from the tag's `crates/pdf/examples/test-signer/`. The signer is read through
the contents API because the release does not package it; it is a throwaway fixture, not a
credential.

| File | SHA-256 |
|---|---|
| `test-signer/cert.der` | `875f33fbb5456fa2b8944a6367ee363494790be12925c4d56ff06aeb4ce0e5cc` |
| `test-signer/key.der` | `cfadbce705b0661378f5b3c96b95c94a2817d76ea1e364a254bfdbcbe4ddda42` |

### Packaged runtime identifiers

Checksums as staged from `prism-pdf-natives-v0.4.0.tar.gz`. Re-running the fetch script prints this
table, so it can be pasted rather than computed.

| RID | Library | SHA-256 |
|---|---|---|
| `win-x64` | `pdf_ffi.dll` | `ac6c11a00b8b4c5af296eec611f659c3a31db7467812488e21d099247274b454` |
| `win-x86` | `pdf_ffi.dll` | `8457105be74709e54014750fdfecbd26d7048a05527322f4842fea77448b8860` |
| `win-arm64` | `pdf_ffi.dll` | `4f7b4b82fe4eba3d282b57017e70070a7c88dba64978366fbd7883b1cb4ffa84` |
| `linux-x64` | `libpdf_ffi.so` | `f6d3340772753c4ac1ba568eed0f38d8322e2a98ef5bf73f8ce0438b989e0998` |
| `linux-arm64` | `libpdf_ffi.so` | `8846827cea3bff5ee14ea94469b39278302f5b716e726c3cf3ecb466c55490b7` |
| `linux-arm` | `libpdf_ffi.so` | `6fb04b6c1ad6c9af83a7e9fbc387afc26a9be23fa4d3021ecbde7d287bfd660a` |
| `linux-musl-x64` | `libpdf_ffi.so` | `11bbd8e448ed18e5b7d5c769b16802556c4109745aa3042b7913307cb8664ef0` |
| `linux-musl-arm64` | `libpdf_ffi.so` | `a778fa53a51a0b40f2ac06c8997e23ab26a8771271e4c650e368a1397cf3f7cd` |
| `osx-x64` | `libpdf_ffi.dylib` | `62a8bc52fab20eda156e2652df19397cd38cd5ccb8295c2be9784ecdd5099878` |
| `osx-arm64` | `libpdf_ffi.dylib` | `eeafb40c9e29b265abb1d03675d3bd6d4d42909659aec6d0ae734df9fea84d62` |

The core also builds `android-arm`, `android-arm64` and `android-x64`, and this package
deliberately omits them: a netstandard2.0 assembly cannot load them — .NET for Android needs a
`net8.0-android` target framework — so packaging them would add 12 MB nothing can reach. Add them
when that target is added, not before. The Apple platforms are static slices in
`PrismPDF.xcframework` and need a different mechanism again (`DllImport("__Internal")` and
multi-targeting); see `docs/roadmap.md`.

Header exports at this version: **386**. Bound by this SDK: see
`build/gen_native_methods.py` and the coverage report `NativeSurfaceTests` prints.
