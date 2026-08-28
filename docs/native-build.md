# The native library

This SDK links against **`pdf_ffi`**, the C ABI's shared library. There are two ways to get one,
and for almost everyone it is the first.

## Fetching the published build

The core publishes a prebuilt library per platform on every `v*` tag, so this repository does not
cross-compile Rust and you do not need a Rust toolchain to work on the binding:

```bash
build/fetch-natives.sh --corpus
```

That downloads the bundle for the tag in `Directory.Build.props`, verifies the published files
against `SHA256SUMS-<tag>.txt` and then every file inside the bundle against its own `SHA256SUMS`,
**refuses to continue if the bundled header is not the one `native/include/prismpdf.h` holds** —
that header is what the raw P/Invoke layer was generated from, so a mismatch means every signature
is a guess — and stages `native/runtimes/<rid>/native/`. `--corpus` also fetches the shared
conformance inputs, which is what lets the suite run with no core checkout at all.

It needs an authenticated `gh`: the core repository is private.

Ten runtime identifiers are staged, and `native/VENDORED.md` records a checksum for each. The
libraries are not committed — they are downloaded, verified, and packaged.

**There is no core checkout in this repository.** `--corpus` also reads the core's signing fixture
out of the pinned tag, so the whole suite — all 84 tests — runs against downloaded inputs alone.

## Building from source

The rest of this document. It is the right path when you are working on the engine itself, or on a
platform the published matrix does not cover.

> Do not confuse `pdf_ffi` with `prismpdf`. The latter is the core's CLI binary and is not what any
> binding loads.

| Platform | Artifact |
|---|---|
| macOS | `libpdf_ffi.dylib` |
| Linux | `libpdf_ffi.so` |
| Windows | `pdf_ffi.dll` |

A static `libpdf_ffi.a` / `pdf_ffi.lib` is produced too; the .NET loader wants the dynamic one.

## Prerequisites

- **Rust 1.88+ (edition 2024)**, which the core requires. Install with:
  ```bash
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
  ```
- **A .NET SDK 9.0.200 or newer** to build the repository: the solution is `PrismPdf.slnx`, and
  the XML solution format is only understood from that version on. An 8.0.x SDK fails with
  `MSB1003: Specify a project or solution file`. `global.json` asks for `8.0.0` with
  `rollForward: latestMajor`, so a 9.x or 10.x SDK satisfies it.
- **The .NET 8 runtime**, because `tests/PrismPdf.Tests` targets `net8.0`. The SDK library itself
  targets netstandard2.0, so *consuming* it needs only a netstandard2.0-compatible runtime.
- A `prism-pdf` checkout at the tag this binding is pinned to — see
  [`../native/VENDORED.md`](../native/VENDORED.md):
  ```bash
  git clone --branch v0.4.0 git@github.com:theunloop/prism-pdf.git
  ```

There are no cargo features to choose; `pdf-ffi` builds one way.

## Build and stage

```bash
build/build-native.sh                       # ./prism-pdf, release profile
build/build-native.sh --core ../core --debug
```

The script builds `cargo build -p pdf-ffi --release`, copies the artifact to `native/lib/`, and
tells you if the vendored header has drifted from the checkout's. Header drift is a *note*, not an
error: the ABI is append-only, so an older vendored header keeps working against a newer library.
It does mean there is new surface worth binding.

Then point the loader at the staged library:

```bash
export PRISMPDF_NATIVE_PATH="$PWD/native/lib"
dotnet test
```

## How the loader finds the library

`NativeLibraryResolver` runs at module load and probes, in order:

1. `PRISMPDF_NATIVE_PATH` — either the directory holding the library or the full path to the file.
2. The platform-specific filename next to the assembly.
3. `runtimes/<rid>/native/<filename>` next to the assembly — the packaged NuGet layout.
4. `native/lib/<filename>` next to the assembly.
5. The .NET default probing rules, which also cover the packaged layout.

Step 3 and step 5 reach the same folder by different routes, and both are needed. The default
rules resolve it from the application's `deps.json`, so they work only on a host that has one; a
.NET Framework or Mono application, or anyone who copied the package layout out of the NuGet
cache by hand, has no `deps.json` to read. Probing the folder directly is what lets one package
serve every host.

### How the identifier is chosen

The `<rid>` in step 3 is computed from **`RuntimeInformation.ProcessArchitecture`**, not the OS's.
Bitness of the *process* is what a native library has to match: an x86 process on x64 Windows needs
the x86 build, and an x64 process emulated on Windows ARM64 needs the x64 one. Asking the OS gets
both wrong.

On Linux the libc flavour is part of the identifier, because a glibc build genuinely cannot load on
musl. netstandard2.0 exposes no API for this, so musl is detected by its loader
(`/lib/ld-musl-*.so.1`). A musl host probes `linux-musl-<arch>` first and plain `linux-<arch>`
second, so a package shipping only the glibc build still gets attempted rather than being ruled out
before the loader has a say.

`LoaderTests` covers all of this, and needs neither a native library nor a corpus.

### Why the resolver installs itself reflectively

The hook it needs — `NativeLibrary.SetDllImportResolver` — is .NET Core 3.0+, and this SDK targets
netstandard2.0. So the resolver looks the API up by name at module load and uses it when the
*hosting* runtime has one. Every .NET Core 3.0+, .NET 5+ and .NET 8+ host does, which is where the
probing order above applies in full.

On a runtime without it — .NET Framework, and some Mono configurations — there is no hook to
install, and the resolver instead **pre-loads** the first candidate it finds through the platform
loader (`LoadLibraryW`, or `dlopen`) before the first P/Invoke binds:

| Host | `PRISMPDF_NATIVE_PATH` | Notes |
|---|---|---|
| .NET Core 3.0+, .NET 5+ | ✅ full probing order | The resolver hook is installed. |
| .NET Framework | ✅ works | `LoadLibraryW` of a full path means the later `DllImport("pdf_ffi")` binds to the module already in the process. |
| Mono on Unix | ⚠️ not guaranteed | Mono may resolve the bare name through its own search path instead. Put the library beside the application, or use `LD_LIBRARY_PATH` / `DYLD_LIBRARY_PATH`. |

Because step 3 probes the packaged layout without going through `deps.json`, these hosts reach it
too — the pre-load path walks the same candidate list. Mono stays the weak case, and for the same
reason as before: the pre-load succeeds, but Mono may still resolve the bare `pdf_ffi` name through
its own search path rather than binding to the module already in the process.

### dlopen on musl

The Unix pre-loader calls `dlopen` through `libdl.so.2`, then a bare `libdl`, then `libc`. The last
is there for musl (Alpine), which has no `libdl` at all — it implements `dlopen` inside libc. Without
that entry `PRISMPDF_NATIVE_PATH` silently does nothing on Alpine, while the packaged layout keeps
working via the default rules, which is the most confusing possible failure: the environment
variable appears to be ignored.

## What the NuGet package carries

`dotnet pack` puts the staged libraries at `runtimes/<rid>/native/` inside the `.nupkg` — the
layout NuGet itself understands. One package carries all ten:

```
PrismPdf.<version>.nupkg   ~20 MB
  lib/netstandard2.0/PrismPdf.dll
  build/PrismPdf.targets
  runtimes/{win-x64,win-x86,win-arm64,linux-x64,linux-arm64,linux-arm,
            linux-musl-x64,linux-musl-arm64,osx-x64,osx-arm64}/native/
```

Splitting it per platform would save a consumer nothing: NuGet downloads a package whole, so
RID-awareness is asset *selection* at build time, not a smaller download. What it would cost is
making every consumer name their platform in a `PackageReference`. The real cost of one package is
output size on a build with no `RuntimeIdentifier` — all ten land in `bin/`, about 44 MB. A
RID-specific `dotnet publish` carries exactly one, around 4 MB, flattened beside the application.

`build/PrismPdf.targets` ships inside the package and exists for the hosts that never unpack
`runtimes/` at all. The .NET SDK resolves those assets through the application's `deps.json`;
.NET Framework and some Mono configurations have none, so the correct binary sits in the package
and is never seen — a `DllNotFoundException` from a package that demonstrably contains the library.
The targets file copies the Windows RIDs to the output directory, where step 3 of the probing order
finds them. It deliberately does not pick a RID at build time: `AnyCPU` says nothing about the
bitness the process will have, and `ProcessArchitecture` at run time does.

`build/smoke-package.sh` packs, installs the result into a throwaway project from a local feed, and
runs a real document through it. It is the only check of any of this — every other test reaches the
library through a `ProjectReference`, which never exercises NuGet's asset selection.

## Without a native library

The SDK still builds, and so does the test project. The suites that call into the engine derive
from `NativeTestBase`, whose one-time setup skips the fixture with an explanatory message when
`pdf_ffi` cannot be loaded. `NativeSurfaceTests` and `CompatTests` are pure managed code and keep
running — the former catches a stale raw layer, the latter covers the netstandard2.0 substitutes —
and neither needs a toolchain at all.

That split is deliberate: a contributor without Rust installed can still change and verify the
binding layer.

## Troubleshooting

**`DllNotFoundException: pdf_ffi`** — the library is not on any probed path. Check
`echo $PRISMPDF_NATIVE_PATH` and that the file actually exists there.

**macOS refuses to load it** — a library built on the same machine is fine; one downloaded from CI
may be quarantined. `xattr -d com.apple.quarantine native/lib/libpdf_ffi.dylib`.

**Architecture mismatch** — an x64 library will not load into an arm64 process. Build on the
machine you run on, or build for the target explicitly with `cargo build --target`.

**`cargo: command not found`** — the build script says exactly this and how to fix it, but note
that a fresh `rustup` install only affects new shells; `source "$HOME/.cargo/env"` in the current
one.

## CI

`.github/workflows/ci.yml` is five jobs:

| Job | What it establishes |
|---|---|
| `managed` | The raw layer is not stale (`gen_native_methods.py --check`), and the three fixtures that need no engine pass |
| `natives` | The published bundle for `PrismPdfCoreTag` downloads and verifies; uploads it once for everyone |
| `conformance` | The suite passes on `linux-x64`, `linux-arm64`, `osx-arm64`, `win-x64` and **Alpine** |
| `package` | `smoke-package.sh` runs, and the `.nupkg` really contains all ten RIDs |
| `publish` | On a `v*` tag only, behind the `nuget` environment |

Two details worth keeping. `natives` is the only job holding a credential for the core repository,
so every other job runs on bytes that were checksum-verified exactly once. And `conformance` fails
on a **skipped** test, not only a failing one: native suites skip when `pdf_ffi` cannot be loaded,
which is the right courtesy for a contributor with no engine and precisely the wrong outcome in
CI — a green leg that tested nothing is indistinguishable from one that tested everything.

The Alpine leg matters more than its size suggests. musl needs its own build and reaches `dlopen`
through libc rather than `libdl`; until that leg existed, both were reasoned rather than observed.

### Publishing

`publish` authenticates with [Trusted Publishing][tp] rather than a stored API key: GitHub mints a
short-lived OIDC token, nuget.org validates it against a policy and exchanges it for a key valid
for one hour. Nothing long-lived is kept in this repository, and there is no 365-day key rotation
to forget.

The policy is created on nuget.org under *username → Trusted Publishing* and must match the
workflow exactly:

| Field | Value |
|---|---|
| Repository Owner | `theunloop` |
| Repository | `prism-pdf-dotnet` |
| Workflow File | `ci.yml` — the file name only, not the path |
| Environment | `nuget` |

The job needs `permissions: id-token: write`, without which the token is never issued, and one
secret, `NUGET_USER` — the nuget.org *profile name*, not an email address. It is a secret only to
keep it out of logs.

**A new policy on a private repository is temporarily active for seven days.** nuget.org pins a
policy to the numeric repository and owner IDs so that deleting and recreating a repository under
the same name cannot inherit it, and it only learns those IDs from a real publish. Publish once
inside the window and the policy becomes permanent; miss it and the policy goes inactive until the
window is restarted.

[tp]: https://learn.microsoft.com/nuget/nuget-org/trusted-publishing
