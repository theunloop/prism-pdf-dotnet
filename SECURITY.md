# Security Policy

This SDK is a **binding**. Almost every byte of a PDF is parsed by the Rust engine across the C
ABI, not by this repository — so most vulnerabilities in "Prism PDF for .NET" are really
vulnerabilities in [Prism PDF](https://github.com/theunloop/prism-pdf), and the two projects
have different threat models. Reporting to the right one gets a fix faster.

## Supported versions

Pre-1.0 (`0.x`). Only the latest released version and `main` receive security fixes. Each release
records the core version it binds in [`CHANGELOG.md`](CHANGELOG.md) and
[`native/VENDORED.md`](native/VENDORED.md); a fix in the engine reaches you through a new release
of this package built against the newer core.

## Reporting a vulnerability

**Please do not open a public issue.** Use GitHub's private vulnerability reporting — the
**Security → Report a vulnerability** tab — which opens an advisory visible only to maintainers.

If you are not sure which repository is at fault, report it here and we will forward it. A
crashing or hanging input file is a perfectly good report on its own; you do not need to work out
where the fault lies.

Please include, as far as you can: the package version, the core version it reports
(`Pdf.Version`), your platform and runtime, a minimal reproducing input, and what happens.

## What is in scope for this repository

The binding owns the boundary, not the parsing. These are ours:

- **Memory safety in the interop layer.** The idiomatic layer is the only place ownership is
  enforced (see [`docs/ownership.md`](docs/ownership.md)). A use-after-free, double-free, or a
  handle that survives disposal is a vulnerability here — including any way to reach one from
  safe, public API use.
- **A native pointer reaching user code**, or any way for user code to hand an arbitrary pointer
  to the ABI. Handles are `internal` precisely so this is unrepresentable; a report showing
  otherwise is high severity.
- **Misreported cryptographic results.** If `VerifySignatures` reports integrity or trust that the
  engine did not actually report — a status misread, a truncated buffer, a diagnostic attributed
  to the wrong call — that is a binding bug with security consequences, even though the
  cryptography is upstream.
- **The supply chain of the package itself**: the fetched native libraries, their checksums, and
  anything that would let a package be built carrying a binary other than the published one.

## What belongs upstream

Report these to [Prism PDF](https://github.com/theunloop/prism-pdf) directly — its `SECURITY.md`
covers them, and a fix there benefits every language binding at once:

- Crashes, hangs, unbounded memory, or pathological CPU from a malformed or hostile PDF.
- Panics or aborts crossing the FFI boundary.
- Cryptographic flaws: a signature that verifies when it should not, or an encryption defect.
- Bypasses of the engine's anti-DoS limits.

A file that merely *fails to parse* is a bug rather than a vulnerability — unless it fails by
crashing, hanging, or exhausting memory instead of returning a clean error.

## A note on the loader

`PRISMPDF_NATIVE_PATH` makes the SDK load a native library from a path of the caller's choosing.
That is intended, and it is not a privilege boundary: a process that can set that variable can
already load code into itself by other means. If you are running untrusted code in your process,
this variable is not the thing standing between you and it.
