# Contributing

Thank you for looking. This repository is a **binding**, not a PDF engine, and that changes where
most contributions belong — please read the first section before opening a pull request, because
the honest answer to "where does my change go?" is often "the other repository".

## Where does this change belong?

| You want to… | Where |
|---|---|
| Fix PDF parsing, writing, fonts, crypto, layout | **[Prism PDF](https://github.com/theunloop/prism-pdf)** — the engine. Nothing here parses a PDF. |
| Add a capability the ABI does not expose yet | **Prism PDF** first. This SDK can only bind what `prismpdf.h` declares. |
| Bind ABI surface that exists but is not exposed in C# | **Here.** See [`docs/roadmap.md`](docs/roadmap.md) for what is unbound and in what order. |
| Fix ownership, disposal, error handling, or the loader | **Here.** That is what this repository is. |
| Improve naming, docs, or the idiomatic API shape | **Here**, but check [`docs/naming.md`](docs/naming.md) first — the rules come from upstream. |

If you are unsure, open an issue here and we will route it.

## Three rules that are not obvious

**`src/PrismPdf/Interop/NativeMethods.cs` is generated. Never hand-edit it.**
`build/gen_native_methods.py` emits it from the vendored header, and
`python3 build/gen_native_methods.py --check` fails CI if the file and the header disagree. To bind
a new area, add its export names to the `AREAS` list in that script, regenerate, and write the
idiomatic layer for them.

**Do not fork the test corpus into this repository.** The suite reads the core's shared
`corpus/{valid,malformed,edge}`, downloaded from its release. Every binding asserting the same
facts against the same bytes is the point; a local copy would let them drift silently.

**Keep the two layers apart.** The raw layer is a flat, mechanical projection with no logic. The
idiomatic layer is the *only* place ownership and error conventions are enforced. A `Marshal` call
or a status check leaking into the raw layer, or a raw `nint` reaching public API, is the thing
review will catch first. [`docs/architecture.md`](docs/architecture.md) has the reasoning.

## Getting set up

```bash
build/fetch-natives.sh --corpus     # the engine, corpus and fixtures — all checksum-verified
dotnet test                          # 84 tests, no environment variables needed
```

You need a **.NET SDK 9.0.200+** (the solution is `.slnx`), the **.NET 8 runtime**, and an
authenticated [`gh`](https://cli.github.com). No Rust toolchain: the engine is downloaded, not
built. [`.devcontainer/`](.devcontainer) has it all configured.

> **The core repository is currently private.** Until that changes, `fetch-natives.sh` needs an
> account with read access, and without it 60 of the 84 tests skip. The managed fixtures — header
> parity, the netstandard2.0 substitutes, and the loader's probing rules — run for everyone, so
> work on those layers is possible without engine access. We know this is a poor contributor
> experience and it is the main thing standing between this repository and being properly open.

## Before you open a pull request

```bash
python3 build/gen_native_methods.py --check    # the raw layer is not stale
dotnet build                                    # warnings are errors, and so are style violations
dotnet test                                     # all 84, no skips
build/smoke-package.sh                          # only if you touched packaging
```

`TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are both on, so an `.editorconfig` violation
fails the build rather than showing up in review.

**New binding surface needs a test in the journey it belongs to**, not a new fixture of its own.
The suite is a port of the core's conformance journeys — see
[`docs/conformance-suite.md`](docs/conformance-suite.md) — and keeping that shape is how the
bindings stay comparable to each other.

Note that CI cannot run in full on a pull request from a fork: GitHub withholds secrets from fork
workflows, so the jobs that need the engine will fail. A maintainer will run them. Do not take
that failure as a comment on your change.

## Commit messages

Explain *why*, not *what* — the diff already says what. Present tense, imperative mood. If a change
looks wrong without context, that context belongs in the message rather than in a review comment
that no one will find again in a year.

## Reporting bugs

Include the package version, the engine version (`Pdf.Version`), your platform and runtime, and
ideally a PDF that reproduces it. If the file is sensitive, say so and we will find another way.

**Security issues do not go in the issue tracker** — see [`SECURITY.md`](SECURITY.md).
