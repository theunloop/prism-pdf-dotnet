# The conformance suite

> "A binding's test suite is a port of the same journeys, against the same inputs, asserting the
> same facts — that is what 'the binding works' means, and it is also how the binding *tests the
> SDK* from a real consumer's seat."
> — the core's binding author's guide

## The shared corpus

The suite reads the core's `corpus/{valid,malformed,edge}` rather than forking those bytes.
Forking would let the two drift, and the whole point of a shared corpus is that they cannot. The
core publishes it per tag as `prism-pdf-corpus-vX.Y.Z.tar.gz`, which `build/fetch-natives.sh
--corpus` stages into `native/corpus/`; failing that the tests look for a `prism-pdf/corpus`
checkout above the test output directory, or use `PRISMPDF_CORPUS` if set. With none of the three
they skip with a message rather than failing.

One fixture is not in that artifact. The signing journey needs the core's throwaway test signer
(`crates/pdf/examples/test-signer/`), which exists only in the source tree — so `--corpus` reads
those files out of the pinned tag through GitHub's contents API and stages `native/test-signer/`
rather than making a 400 MB clone the price of two tests. `TestSigner` looks there first and
honours a checkout second. The right fix is upstream: three small files in the corpus tarball
would remove the special case for every binding.

| Directory | Holds | Asserted here |
|---|---|---|
| `valid/` | Well-formed PDFs from various producers | Opens strictly, page count > 0, `save → reopen` preserves pages and text |
| `malformed/` | Corrupt or missing xref, wrong `/Length`, truncated | Opens **or** fails with `Parse` — never crashes; a recovered open reports why |
| `edge/` | Legal-but-unusual: indirect `/Length`, object streams, deep nesting | Opens and round-trips |

### Opening a malformed file is not the same as recovering it

`OpenMode.Recovered` means one specific thing: the cross-reference itself had to be rebuilt, which
upstream is `RecoveryReason::XrefParseFailure` or `UnreachableCatalog`. Leniency *below* the xref
layer does not set it, and two files in `malformed/` rely on exactly that — `wrong-length.pdf` has
an intact xref and a short `/Length` the parser resolves by scanning to `endstream`, and
`garbage-prefix.pdf` has junk before `%PDF` but absolute xref offsets that still resolve. Both open
in `Strict` mode, correctly.

So `MalformedCorpus_RecoversOrFailsCleanly` asserts only *opened or `Parse`, never a crash*, and
scopes the diagnostics check to a recovered open. `BrokenXref_OpensRecoveredAndReportsWhy` is what
holds recovery itself to account, over the three files whose xref really is broken; without it the
suite would still pass if the engine stopped recovering and simply refused them.

## Journeys and their status

| Journey | Upstream guide | Fixture | Status |
|---|---|---|---|
| **Vertical slice** | "The vertical slice" | `VerticalSliceTests` | ✅ all 9 steps |
| **Parse** | `parse.md` | `ParseJourneyTests` | ✅ |
| **Manipulate** | `manipulate.md` | `ManipulateJourneyTests` | ✅ |
| **Failure paths** | `errors-limits.md` | `FailurePathTests` | ✅ |
| **Security** | "Security" | `SecurityJourneyTests` | ✅ encrypt, permissions, sign, verify |
| **Create** | `create.md` | — | ⬜ blocked: `Builder` not bound |
| **Compose** | `compose.md` | — | ⬜ blocked: `Composition` not bound |
| **Raw-layer completeness** | "its completeness check is the analogue of `header_surface.c`" | `NativeSurfaceTests` | ✅ |
| **netstandard2.0 substitutes** | — (this SDK's own) | `CompatTests` | ✅ |
| **Native library probing** | — (this SDK's own) | `LoaderTests` | ✅ |

## The vertical slice

The nine steps the guide prescribes, in order, each one convention exercised once:

| Step | Exercises | Test |
|---|---|---|
| `Pdf.Version` → `"0.4.0"` | static string, never freed | `Version_ReportsTheEngineVersion` |
| `Document.Open(valid)` | owned handle; disposal frees it | `Open_ValidDocument_Succeeds` |
| `Document.Open(garbage)` | `Parse` + a diagnostic message | `Open_Garbage_ThrowsParse` |
| `Document.Open(encrypted, "wrong")` | `Password` as its own status | `Open_EncryptedWithWrongPassword_ThrowsPassword` |
| `doc.PageCount` | plain value out-param | `PageCount_ReadsAPlainOutParam` |
| `doc.PageText(0)` | owned string, copied + freed | `PageText_ReturnsAnOwnedString` |
| `doc.PageText(n + 1)` | `NotFound` from an index **is** an error | `PageText_PastTheLastPage_ThrowsNotFound` |
| `doc.Save()` | owned buffer, copied + freed | `Save_ReturnsAnOwnedBuffer` |
| reopen the bytes | round trip through the binding itself | `Save_RoundTripsThroughTheBinding` |

The assertion is on the tag itself. The core's release workflow refuses to build a `vX.Y.Z` tag
whose workspace version disagrees, which turns "the ABI version equals the release tag" into a
checked property a binding may pin against. See `native/VENDORED.md`.

## The semantic contracts

`FailurePathTests` is the fixture that keeps the idiomatic layer honest. Each of the guide's six
contracts has a test:

| Contract | Test |
|---|---|
| 1. One error type, carrying the stable status | `Failure_RaisesOneExceptionTypeWithAStatus` |
| 2. `NotFound` on an optional getter is absence | `AbsentOptionalField_IsNullNotAnError` |
| 2. …but an out-of-range index is an error | `OutOfRangeIndex_IsAnError` |
| 3. A dead handle raises, not crashes | `DisposedDocument_ThrowsObjectDisposed` |
| 4. Borrowed items keep their owner alive | `BorrowedItem_AfterListDisposed_ThrowsObjectDisposed` |
| 5. Copy, then free, immediately | `OwnedStringsAndBuffers_AreCopiedNotAliased` |
| 6. No shared mutable handles across threads | documented, not testable as a positive |

Null-argument and double-free defence is already proven on the Rust side
(`crates/pdf-ffi/src/api/null_sweep.rs`), and the guide says bindings need not re-sweep it "because
their idiomatic layer makes those states unrepresentable". This SDK's layer does: handles are
`internal`, so user code cannot pass a null or freed pointer to anything.

## The raw-layer completeness check

The C analogue compiles a file taking the address of every export. In C# the compiler already
proves each `[DllImport]` is well-formed, so the interesting failure is the other direction: a
declaration that no longer matches the vendored header. `NativeSurfaceTests` therefore parses
`native/include/prismpdf.h` and asserts:

1. every raw-layer declaration still exists in the header;
2. every declaration pins the cdecl calling convention and `ExactSpelling`;
3. the coverage gap is *reported* — printed, grouped by area, not failed. The ABI is append-only,
   so unbound exports are a roadmap item rather than a defect, and printing them on every run keeps
   the gap visible instead of letting a roadmap document go stale.

## Running

```bash
build/fetch-natives.sh --corpus    # then dotnet test needs no environment at all
dotnet test

dotnet test --filter "FullyQualifiedName~VerticalSliceTests"

# The three fixtures that need no native library and no Rust toolchain:
dotnet test --filter "FullyQualifiedName~NativeSurfaceTests"
dotnet test --filter "FullyQualifiedName~CompatTests"
dotnet test --filter "FullyQualifiedName~LoaderTests"
```

## The anchor test that is still missing

The guide names one specific acceptance test: a port of
`crates/pdf-ffi/tests/c/compose_invoice.c`, the standalone C consumer that builds a tagged
multipage invoice through the composition API. *"Every binding builds the same invoice and asserts
on it by reopening."*

That is the gate for the compose area, and it is the single best measure of when this SDK is
done — it exercises composition, tagging, pagination, tables, and images in one artefact. It cannot
be written until the composition surface is bound; see [roadmap.md](roadmap.md).
