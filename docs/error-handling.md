# Errors

## One exception type

Every non-`Ok` status becomes a `PrismPdfException` carrying the stable integer status and, where
the library had one, the diagnostic message:

```csharp
try
{
    using var doc = Document.Open(bytes);
}
catch (PrismPdfException ex) when (ex.Status == PrismPdfStatus.Password)
{
    // Encrypted, and the password was wrong or absent. Retry with one.
}
catch (PrismPdfException ex) when (ex.Status == PrismPdfStatus.Parse)
{
    // Unreadable even after recovery.
    logger.LogWarning("{Message}", ex.Message);
}
```

Switch on `ex.Status`, not on the message. The status values are append-only and never renumbered;
messages are diagnostics and may change.

| Status | Meaning |
|---:|---|
| `Ok` | Success — never thrown. |
| `NullArgument` | A required pointer argument was null, or a value was rejected (e.g. an unknown encryption algorithm). |
| `Parse` | Unparseable even after recovery. |
| `NotFound` | The item does not exist. **Read the next section** — this one has two readings. |
| `Internal` | Internal error, including a caught Rust panic. Panics never cross the boundary. |
| `Password` | Encrypted, and the supplied password is wrong. |
| `Conformance` | A conformance pass refused the document: nothing malformed, a standard's rule unmet. |
| `InvalidUse` | A handle is stale, its owner was released, or it was already finalised. |
| `Layout` | Composition rejected geometry or could not paginate. |

## `NotFound` is two different things

This is the single most important distinction in the ABI, and the SDK draws it for you:

**On an optional getter, `NotFound` is absence.** No exception; the property returns `null`.

```csharp
doc.Info("Producer")   // string?  — null when the key is absent
doc.Xmp                // string?  — null when there is no XMP packet
doc.CreationDate       // PdfDate? — null when unset
font.ProgramFormat     // FontFormat? — null when the font is not embedded
signature.IsTrusted    // bool?    — null when trust was never evaluated
```

`signature.IsTrusted` is the clearest example of why this matters: `null` means *nobody checked*,
`false` means *checked, and not trusted*. Collapsing those two into `false` would silently turn "we
did not verify" into "we verified and it failed".

**On an index lookup, `NotFound` is an error.** A page past the end throws:

```csharp
doc.PageText(9999);   // throws PrismPdfException(Status = NotFound)
```

## Where the message comes from

After a guarded call fails, the library keeps a **thread-local** diagnostic. `PrismPdfException.Create`
clones it immediately — same thread, no intervening call — because the next successful guarded call
clears the slot.

There is one subtlety the SDK handles for you. Argument checks that reject *before* entering a
guarded operation report their status directly and deliberately do **not** replace the slot, so
that a secondary cleanup mistake cannot erase the actionable failure that preceded it. The SDK
therefore only uses the message when the snapshot's status matches the call that just failed, and
otherwise falls back to the status name. `ex.HasDiagnostic` tells you which happened.

The practical consequence for your code: **do not** put an `await`, a `Task.Run`, or a thread hop
between a native call and reading its error. The SDK never does; if you extend it, don't either.

## Managed exceptions the SDK raises

Not everything is an ABI failure. These come from the wrapper, before any native call:

| Exception | When |
|---|---|
| `ObjectDisposedException` | Using a disposed handle, an item whose list was disposed, or a handle a consuming call took ownership of. |
| `ArgumentNullException` | A required argument was null. |
| `ArgumentOutOfRangeException` | A negative index, or a version part outside 0–255. |
| `ArgumentException` | An empty page-index list, an empty document list to merge, a null entry inside a list. |
| `PrismPdfHandleAllocationException` | A constructor returning a bare pointer returned null. |
| `DllNotFoundException` | `pdf_ffi` could not be loaded — see [native-build.md](native-build.md). |
