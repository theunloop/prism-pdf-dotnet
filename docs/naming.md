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

`prismpdf_version` → `Pdf.Version`; `prismpdf_merge` → `Pdf.Merge(...)`; the three PDF/A level
helpers → `Pdf.PdfAPart`, `Pdf.PdfAAllowsAttachments`, `Pdf.PdfACode`.

> **Where rule 6 and rule 2 collide.** `prismpdf_measure_text` and `prismpdf_wrap_text` read like
> module-level functions — but their first parameter is a
> `PrismPdfTextBlock` handle, so rule 2 governs and they are `block.MeasureText(text)` and
> `block.WrapText(text, width)`. The signature decides, not the name.

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

Five, each forced by C# rather than chosen.

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

### 4. `PrismPdfObject` becomes `PdfObject`, and the flow's verbs take an `Add` prefix

Two names the mechanical mapping produces do not survive contact with C#.

`prismpdf_object_*` would give a type called `Object`, which cannot coexist with `System.Object` in
a language where every type derives from it. It is `PdfObject`, the same reshaping `PdfDate` gets.

`prismpdf_flow_table` would give `flow.Table(table)` — a method whose name is also the name of its
parameter's type, which is legal C# and reads as a mistake. Once that one has to change, the whole
family changes with it, because `flow.Text(...)` beside `flow.AddTable(...)` is worse than either
convention applied consistently. So every flow call that *appends* content takes an `Add` prefix:
`AddText`, `AddHeading`, `AddList`, `AddTable`, `AddImage`, `AddFigure`, `AddNote`, `AddFormula`,
`AddSpace`. Calls that *set* document-level state keep their names: `SetHeader`, `SetFooter`,
`SetTagged`, `SetInfo`.

The same `Set…`/`Add…` split is load-bearing in `CompositionContainer`, where it is not cosmetic:
`Set…` fills a slot and spends the handle, `Add…` appends to a container and does not.

### 5. `PrismPdfConformanceException` subclasses the one error type

Semantic contract 1 asks for one error type. `PrismPdfStatus.Conformance` is the one status that
carries a second value — *which* rule was unmet — so it arrives as a subclass carrying a
`ConformanceIssue` rather than losing that value or bolting a nullable field onto every exception.
`catch (PrismPdfException)` still catches everything, which is what the contract is for.

## Additions

Two conveniences that are *not* ABI entry points, both documented as such in their XML docs:

| C# | What it is |
|---|---|
| `Document.OpenFile(path)` | `File.ReadAllBytes` + `Document.Open`. The engine only ever opens from memory. |
| `EncryptionAlgorithm` | A C# enum over the plain `uint32_t` the `SaveEncrypted…` family takes. The values are the ones `docs/ABI.md` defines; the ABI has no `#[repr(C)]` enum here. |
| `PdfSize`, `PdfMargins` | The `const double *` pairs `flow_new` and the page styles take, as record structs — the same reshaping `PdfRect` already applies to a four-double rectangle. |
| `PdfColor` | The three-double `PrismPdfCompositionColor`, kept out of the public API as a layout the way `PdfDate` keeps `PrismPdfDate` out. |
| `PdfReference` | An object number and generation, which the ABI passes side by side everywhere they appear. |
| `XmpMetadata.SetDates` | `CreateDate` and `ModifyDate` formatted from a `DateTimeOffset`, since the ABI takes the ISO 8601 strings XMP itself uses. |
| `PdfObject.Text` | A string or name decoded as text — UTF-16BE with a byte-order mark, UTF-8 otherwise. `Bytes` remains the literal answer. |

Neither invents a noun the ABI lacks, which is the line the guide draws.
