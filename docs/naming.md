# Naming: from C to C#

The core's binding author's guide fixes a mechanical mapping from each C export to its idiomatic
name, so that knowledge transfers between bindings. This page applies those ten rules to C# and
records every place this SDK deviates.

## The rules, applied

**1. Strip the `prismpdf_` prefix.**

**2. The receiver is the first handle parameter, not the name prefix.**

```c
PrismPdfStatus prismpdf_page_text(const PrismPdfDocument *doc, uintptr_t index, char **out_text);
```

takes a document first, so it is `doc.PageText(index)` — the `page_` prefix names the *subject*,
not a receiver type. This is the rule that keeps a `Page` class from appearing.

**3. `<noun>_new*` → constructor; `document_open*` → static factories.**

| C | C# |
|---|---|
| `prismpdf_open_options_new` | `new OpenOptions()` |
| `prismpdf_sign_settings_new` | `new SignSettings()` |
| `prismpdf_document_open` | `Document.Open(bytes)` |
| `prismpdf_document_open_with_password` | `Document.Open(bytes, password)` |
| `prismpdf_document_open_with_options` | `Document.Open(bytes, options)` |
| `prismpdf_document_open_with_private_key` | `Document.OpenWithPrivateKey(bytes, cert, key)` |

**4. `<noun>_free` → the language's disposal idiom, never a public `free`.**

`IDisposable` and `using`. A finalizer exists as a safety net only; deterministic disposal is the
API.

**5. Argument-less getters become properties.**

`prismpdf_document_page_count` → `doc.PageCount`. A getter returning a boolean out-param returns a
plain `bool`.

> **Note.** This rule puts real work behind `doc.Text` and `doc.Xmp`, which .NET convention would
> normally keep as methods. The guide's cross-language consistency wins here, but the XML docs on
> those members say plainly that extraction runs on every read.

**6. Handle-less functions become statics on the top-level class.**

`prismpdf_version` → `Pdf.Version`; `prismpdf_merge` → `Pdf.Merge(...)`.

**7. `*_report` variants get a `…WithReport` companion, never an optional parameter.**

| C | C# |
|---|---|
| `prismpdf_document_save` | `doc.Save()` |
| `prismpdf_document_save_report` | `doc.SaveWithReport()` |

Both exist so the cheap path stays report-free.

**8. Permission helpers become an immutable value type with chainable methods.**

```csharp
Permissions.Restricted.AllowPrint().AllowAccessibility()
```

**9. `#[repr(C)]` enums keep their variant names; the C values are the contract.**

Every value in `Enums.cs` is written out explicitly so a diff against a newer header is a diff of
numbers, not of ordering.

**10. String-pair inputs become the native map type.**

`prismpdf_document_fill_form(doc, names, values, count, …)` → `doc.FillForm(IReadOnlyDictionary<string, string>)`.

## Deviations

Three, each forced by C# rather than chosen.

### 1. The top-level class is `Pdf`, not `PrismPdf`

Rule 6 wants `PrismPdf.version()`. C# cannot give a type the same name as its enclosing namespace
without creating resolution ambiguity at every use site, and `PrismPdf` is the right name for the
namespace. So the statics live on `Pdf`:

```csharp
using PrismPdf;

Pdf.Version          // prismpdf_version
Pdf.Merge(a, b)      // prismpdf_merge
```

### 2. Enum type names drop the `PrismPdf` prefix — except `PrismPdfStatus`

`PrismPdfOpenMode` → `OpenMode`, `PrismPdfColorSpace` → `ColorSpace`, and so on: namespaced, these
read correctly and match rule 9's "minus the prefix" spirit. `PrismPdfStatus` keeps its prefix
because it is the error contract that appears on every exception, and a public type called `Status`
is too generic to live in a shared codebase.

### 3. Value structs are re-shaped, not projected

`PrismPdfDate` and `PrismPdfLimits` stay `internal` in `PrismPdf.Interop`, and the public API exposes
`PdfDate` and `OpenOptions` instead. Projecting a C struct layout into a public .NET API would
freeze that layout into this SDK's compatibility surface for no benefit — and in `PrismPdfDate`'s
case, `PdfDate` can express "this date declares no relationship to UTC" as `int? UtcOffsetMinutes`,
which is what §7.9.4 actually means and what a `bool` + `short` pair only encodes.

## Additions

Two conveniences that are *not* ABI entry points, both documented as such in their XML docs:

| C# | What it is |
|---|---|
| `Document.OpenFile(path)` | `File.ReadAllBytes` + `Document.Open`. The engine only ever opens from memory. |
| `EncryptionAlgorithm` | A C# enum over the plain `uint32_t` the `SaveEncrypted…` family takes. The values are the ones `docs/ABI.md` defines; the ABI has no `#[repr(C)]` enum here. |

Neither invents a noun the ABI lacks, which is the line the guide draws.
