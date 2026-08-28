# Getting started

## Install

There is no package yet. Reference the project directly:

```xml
<ProjectReference Include="path/to/prism-pdf-dotnet/src/PrismPdf/PrismPdf.csproj" />
```

and build the native library once — see [native-build.md](native-build.md). In short:

```bash
git clone --branch v0.4.0 git@github.com:theunloop/prism-pdf.git
build/build-native.sh
export PRISMPDF_NATIVE_PATH="$PWD/native/lib"
```

## Your first program

```csharp
using PrismPdf;

Console.WriteLine($"Prism PDF {Pdf.Version}");

using var doc = Document.OpenFile("input.pdf");

Console.WriteLine($"{doc.PageCount} pages, PDF {doc.Version}");
Console.WriteLine($"Title: {doc.Info("Title") ?? "(none)"}");
Console.WriteLine(doc.PageText(0));
```

Three things in that snippet are worth naming, because they are the shape of the whole API:

- **`using`.** `Document` owns native memory. Dispose it.
- **`doc.Info(...)` returns `string?`.** An absent `/Info` key is absence, not an error. See
  [errors](error-handling.md).
- **`doc.PageText(0)` takes an index on the document.** There is no `Page` type, by design —
  see [architecture](architecture.md).

## Reading

```csharp
doc.PageCount                  // int
doc.Version                    // PdfVersion?  — the declared header version
doc.MinimumVersion             // PdfVersion?  — what the content actually requires
doc.Text                       // whole document, reading order
doc.PageText(i)                // one page, reading order
doc.PageTextPositioned(i)      // one page, layout preserved
doc.Info("Producer")           // string?
doc.Xmp                        // string?  — the raw XMP packet
doc.CreationDate               // PdfDate?
```

Text is decoded through each font's `/ToUnicode` where present, with encoding fallback and composite
CID font support — you get real Unicode, not raw byte codes.

## Inspecting

Each of these returns an owned list. Dispose it, and copy anything you need to keep:

```csharp
using var annotations = doc.PageAnnotations(0);
using var fields      = doc.FormFields();
using var outline     = doc.Outline();
using var attachments = doc.Attachments();
using var fonts       = doc.Fonts();
using var images      = doc.PageImages(0);

foreach (var link in annotations.Where(a => a.Uri is not null))
{
    Console.WriteLine($"{link.Rect} -> {link.Uri}");
}
```

An empty collection is an empty list, not an error: a page with no `/Annots` gives you a list of
zero, and a document with no AcroForm gives you zero fields.

## Manipulating

Every transform returns bytes and leaves the source document untouched:

```csharp
doc.Save()                     // full rewrite, classic xref table
doc.SaveCompact()              // full rewrite, xref stream (PDF 1.5+)
doc.SavePacked()               // full rewrite, object streams — smallest
doc.SaveAs(1, 7)               // exact header version; refuses newer constructs
doc.ExtractPages([2, 0, 0])    // split, subset, reorder, duplicate
doc.RotatePage(0, 90)
doc.SubsetFonts()
Pdf.Merge(first, second)
```

To keep working with the result, reopen it:

```csharp
using var firstPage = Document.Open(doc.ExtractPages([0]));
```

### Knowing what a transform cost

Each has a `…WithReport` companion. A save that succeeds can still have invalidated the
signatures in the source — this is how you find out without re-verifying:

```csharp
using var report = doc.SaveWithReport();

Console.WriteLine(report.RewriteMode);        // FullRewrite
Console.WriteLine(report.SignatureEffect);    // Preserved | Invalidated | Removed
Console.WriteLine(report.StructureEffect);    // Preserved | Removed | Invalidated

File.WriteAllBytes("out.pdf", report.Bytes);
```

## Forms

```csharp
using var fields = doc.FormFields();
foreach (var field in fields)
{
    Console.WriteLine($"{field.Name} ({field.FieldType}) = {field.Value ?? "unset"}");
}

// Fill by fully-qualified name; unknown names are ignored. Emitted as an incremental update.
var filled = doc.FillForm(new Dictionary<string, string>
{
    ["customer.name"] = "Ada Lovelace",
    ["customer.city"] = "London",
});

// Or bake the widgets into page content and drop /AcroForm entirely.
var flat = doc.FlattenForm();
```

## Security

```csharp
// Encrypt, granting everything.
doc.SaveEncrypted("user-password", "owner-password", EncryptionAlgorithm.Aes256);

// Encrypt with narrow permissions and clear-text metadata, as PDF/A wants.
var permissions = Permissions.Restricted.AllowPrint().AllowAccessibility();
doc.SaveEncrypted("user"u8, "owner"u8, permissions, encryptMetadata: false);

// Sign.
using var settings = new SignSettings { Name = "Ada Lovelace", Reason = "Approved" };
var signed = doc.Sign(certificateDer, privateKeyDer, settings);

// Verify — integrity only, unless you supply trust anchors.
using var signatures = doc.VerifySignatures(roots);
foreach (var signature in signatures)
{
    Console.WriteLine($"{signature.Signer}: valid={signature.IsValid}, trusted={signature.IsTrusted}");
}
```

`signature.IsTrusted` is `bool?` deliberately: `null` means trust was never evaluated, `false` means
it was evaluated and the chain is not trusted.

## Opening hostile input

All parsing input is untrusted, and recovery is bounded rather than unlimited:

```csharp
using var options = new OpenOptions { MaxDepth = 64, MaxObjects = 100_000 };
using var doc = Document.Open(bytes, options);

using var report = doc.GetOpenReport();
if (report.Mode is OpenMode.Recovered)
{
    foreach (var diagnostic in report.Diagnostics())
    {
        logger.LogWarning("recovered: {Reason} at {Offset}", diagnostic.Reason, diagnostic.Offset);
    }
}
```

Never widen a limit merely to accept one malformed file. Raise only the bound a trusted operational
requirement implicates, and keep the total object ceiling.

## Creating PDFs

There are three ways to make a document, in increasing order of how much the engine decides for
you.

**Draw it.** A `Content` stream is the PDF operators of §8 and §9, one method each, handed to a
`Builder` as a page:

```csharp
using var content = new Content();
content.BeginText();
content.SetFont("F1", 24);
content.TextMove(72, 700);
content.ShowText("Hello");
content.EndText();

using var builder = new Builder { Title = "Greeting" };
builder.AddPage(content, new Dictionary<string, StdFont> { ["F1"] = StdFont.Helvetica });

File.WriteAllBytes("hello.pdf", builder.Build());
```

The builder survives being built — keep adding pages and build again. `PageSpec` is the longer
form, for a page that needs its own media box or an image resource; `StructNode` builds the logical
structure tree that makes the result tagged.

**Pour it.** A `Flow` wraps lines and breaks pages itself:

```csharp
using var flow = new Flow(PdfSize.A4, PdfMargins.Uniform(72),
    new Dictionary<string, StdFont> { ["F1"] = StdFont.Helvetica });
using var body = new TextBlock("F1", "Helvetica", 11, 14);

flow.SetTagged("en-GB");
flow.AddHeading(1, body, "Quarterly report");
flow.AddText(body, prose);

File.WriteAllBytes("report.pdf", flow.Build());
```

`Build()` and `IntoBuilder()` both **consume** the flow — after either, the handle is dead, and
using it raises `ObjectDisposedException`. `IntoBuilder()` is how a flowed document gets
post-processed: attachments, annotations, a conformance pass.

**Describe it.** A `Composition` is a tree of boxes the engine measures and paginates — the richest
of the three, and the one that produces tagged output most directly. Each handle addresses one
*slot*: a `Set…` fills it and spends the handle, an `Add…` appends to a container and does not.

```csharp
using var composition = new Composition { TaggedLanguage = "en-GB" };
using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
using var column = page.SetColumn(12);

using (var slot = column.AddItem())
{
    using var heading = slot.SetHeading(1);
    heading.SetText("Invoice", 20, 24);
}

File.WriteAllBytes("invoice.pdf", composition.Build());
```

## Conformant output: PDF/A and PDF/UA

A conformance pass finalises a builder as a document that meets a standard, or refuses and names
the rule:

```csharp
using var metadata = new XmpMetadata { Title = "Quarterly report" };
metadata.AddAuthor("Finance");

try
{
    builder.MakePdfUa(metadata, "en-GB");
}
catch (PrismPdfConformanceException ex)
{
    // ex.Issue is FigureWithoutAlt, NotTagged, UnembeddedFont, … — actionable, unlike the status.
}
```

Both standards require every font to be embedded, so a Standard-14 document cannot conform. The
route to a conformant file today is `Flow.EmbedFont` with a real sfnt program, then `IntoBuilder()`
— and the resource name must *not* also be declared as a Standard-14 font in the flow's
constructor, or the pass refuses it as unembedded.

## The escape hatch

When the modelled API has no answer, read the file itself (§7.3):

```csharp
using var catalog = doc.CatalogObject();
using var names = catalog.DictionaryGet("Names");    // null when absent, not an error

using var edit = new Edit(doc);
edit.SetObject(reference, changed);
using var report = edit.Commit(EditCommitMode.Incremental);   // keeps signatures intact
```

Prefer the model when it has an answer: it keeps invariants that hand-edited objects can break.
