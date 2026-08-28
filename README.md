# Prism PDF for .NET

A C# SDK for [Prism PDF](https://github.com/theunloop/prism-pdf) — a pure-Rust PDF engine that reads,
manipulates, and generates PDFs. This repository is the **.NET binding**: it consumes Prism PDF's
stable C ABI (`pdf-ffi`) and projects it into an idiomatic .NET API.

> **Status: early.** The read, manipulate, and security surfaces are implemented and covered by
> the conformance suite. Authoring (`Builder`), layout (`Flow`), and declarative composition are
> **not bound yet** — see [`docs/roadmap.md`](docs/roadmap.md) for the order they land in and
> [`native/VENDORED.md`](native/VENDORED.md) for the exact core release this is pinned to.

```csharp
using PrismPdf;

using var doc = Document.OpenFile("invoice.pdf");

Console.WriteLine($"{doc.PageCount} pages, PDF {doc.Version}");
Console.WriteLine(doc.PageText(0));

// Keep only the first page. The boundary is immutable: `doc` is untouched.
File.WriteAllBytes("first-page.pdf", doc.ExtractPages([0]));
```

## What this SDK is, and is not

It is a **binding**, not a reimplementation. Every capability comes from the Rust engine across a
handle-based C ABI; this repository contributes two things:

1. **A raw layer** (`NativeMethods`) — a flat, mechanical, 1:1 projection of the vendored
   `prismpdf.h`, generated from the header itself so it cannot drift.
2. **An idiomatic layer** — the public API, and the one place the ABI's ownership and error
   conventions are enforced, so user code cannot leak, double-free, or misread a status.

That two-layer shape, the naming rules, and the six semantic contracts all come from the core
repo's **binding author's guide** (`docs/BINDINGS.md`), whose goal is that `doc.PageCount` in C#,
`doc.page_count` in Python, and `doc.pageCount` in JavaScript are recognisably the same call. Where
this SDK had to deviate — C# forbids a type sharing its namespace's name, so the statics live on
`Pdf` rather than `PrismPdf` — the deviation is recorded in [`docs/naming.md`](docs/naming.md).

## Getting started

The SDK targets **.NET Standard 2.0**, so it is consumable from .NET Framework 4.6.1+, .NET Core
2.0+, every .NET 5+ release, Mono, Unity and Xamarin.

The engine ships **inside the package**, one prebuilt library per platform — `win-x64`, `win-x86`,
`win-arm64`, `linux-x64`, `linux-arm64`, `linux-arm`, `linux-musl-x64`, `linux-musl-arm64`,
`osx-x64` and `osx-arm64`. There is nothing to install alongside it and nothing is ever downloaded
at run time.

To build *this repository* you need a **.NET SDK 9.0.200 or newer** — the solution is
`PrismPdf.slnx`, and the XML solution format is not understood by older SDKs — plus the **.NET 8
runtime** to run the test project, and an authenticated [`gh`](https://cli.github.com) to fetch the
engine. [`.devcontainer/`](.devcontainer) has all of it configured.

```bash
# 1. Download the published native libraries and the shared corpus, both checksum-verified.
build/fetch-natives.sh --corpus

# 2. Run the suite. No environment variables: the tests use the same layout the package ships.
dotnet test
```

No Rust toolchain is needed — the core publishes the binaries, and this repository downloads and
verifies them rather than cross-compiling. `build/build-native.sh` is still there for anyone
working on the engine itself.

Without step 1 the SDK still builds and the managed fixtures — header parity, the netstandard2.0
substitutes, the loader's probing rules — still run; the suites that call into the engine skip with
an explanatory message rather than failing.

Full detail, including the package layout and how the loader probes:
[`docs/native-build.md`](docs/native-build.md).

## What you can do today

| Area | Status | Entry points |
|---|---|---|
| **Open** (incl. encrypted, certificate-encrypted, bounded limits) | ✅ | `Document.Open`, `OpenOptions` |
| **Recovery reporting** | ✅ | `Document.GetOpenReport()`, `OpenReport` |
| **Read** — text, positioned text, versions, `/Info`, XMP, dates | ✅ | `Document.PageText`, `.Text`, `.Info`, `.Xmp` |
| **Inspect** — annotations, form fields, outline, attachments, fonts, images | ✅ | `Document.PageAnnotations()`, `.FormFields()`, … |
| **Manipulate** — save (3 modes), save-as-version, extract, rotate, merge, subset fonts | ✅ | `Document.Save*`, `.ExtractPages`, `Pdf.Merge` |
| **Forms** — fill, flatten | ✅ | `Document.FillForm`, `.FlattenForm` |
| **Transform reports** — rewrite mode, signature and structure effects | ✅ | `…WithReport` companions |
| **Encrypt** — RC4/AES-128/AES-256/AES-256-GCM, permissions, public-key, PDF MAC | ✅ | `Document.SaveEncrypted*`, `Permissions` |
| **Sign & verify** — detached CMS, PAdES, timestamps, LTV verification | ✅ | `Document.Sign`, `.VerifySignatures*`, `SignSettings` |
| **Author** — `Builder`, content streams, structure tree | ⬜ Planned | — |
| **Layout** — `Flow`, `Table`, `TextBlock` | ⬜ Planned | — |
| **Compose** — declarative `Composition` | ⬜ Planned | — |
| **PDF/A & PDF/UA conformance production** | ⬜ Planned | — |
| **COS inspection & editing** | ⬜ Planned | — |

The suite prints the exact coverage gap against the vendored header on every run — see
`NativeSurfaceTests.CoverageGap_IsReported`.

## Examples

**Extract every attachment.**

```csharp
using var doc = Document.OpenFile("report.pdf");
using var attachments = doc.Attachments();

foreach (var attachment in attachments)
{
    File.WriteAllBytes(attachment.Name, attachment.Data);
}
```

**Find fonts that are not embedded** — the PDF/A pre-flight check.

```csharp
using var fonts = doc.Fonts();
var unembedded = fonts.Where(f => f.ProgramFormat is null).Select(f => f.BaseFont);
```

**Encrypt with narrow permissions.**

```csharp
var permissions = Permissions.Restricted.AllowPrint().AllowAccessibility();
var bytes = doc.SaveEncrypted("user"u8, "owner"u8, permissions, encryptMetadata: false);
```

**Know what a transform cost you.**

```csharp
using var report = doc.SaveWithReport();

if (report.SignatureEffect is SignatureEffect.Invalidated)
{
    // Valid bytes — but the signatures in them no longer verify.
}
```

## Documentation

| | |
|---|---|
| [Getting started](docs/getting-started.md) | Install, first program, the shape of the API |
| [Architecture](docs/architecture.md) | The two layers, and why the raw one is generated |
| [Naming](docs/naming.md) | C name → C# name, rule by rule, including every deviation |
| [Errors](docs/error-handling.md) | One exception type; when `NotFound` is absence, not failure |
| [Ownership & lifetimes](docs/ownership.md) | Owned vs borrowed, disposal, consuming calls, threading |
| [Building the native library](docs/native-build.md) | Toolchain, platforms, how the loader probes |
| [Conformance suite](docs/conformance-suite.md) | The journeys every binding ports, and their status here |
| [Roadmap](docs/roadmap.md) | What is unbound, in the order it lands |

The authorities this SDK is written against, in order, are in the core repo:
`crates/pdf-ffi/include/prismpdf.h` (each function's exact ownership contract), `docs/ABI.md`, and
`docs/site/ffi-ownership.md`. Where this SDK's docs disagree with those, those win.

## Repository layout

```
build/          build-native.sh, and the raw-layer generator
docs/           this SDK's documentation
native/
  include/      the vendored prismpdf.h (committed; never hand-edited)
  VENDORED.md   which core release it came from, and how to update
src/PrismPdf/
  Interop/      the raw layer — generated P/Invoke, and the loader
  Collections/  owned lists and the borrowed items lent from them
  Compat/       what netstandard2.0 lacks, re-declared internal
  *.cs          the idiomatic layer
tests/          the conformance suite
```

## Contributing

Read [`docs/architecture.md`](docs/architecture.md) first — particularly the rule that
`Interop/NativeMethods.cs` is **generated, not written**. After vendoring a newer header, run
`python3 build/gen_native_methods.py` and review the diff.

Anything the core's binding author's guide got wrong or left ambiguous should be fed back into that
file upstream, as its own final checklist item asks.

## License

[MIT](LICENSE.md), the same as Prism PDF itself.
