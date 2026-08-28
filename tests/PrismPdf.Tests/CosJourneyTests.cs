using System.Text;

namespace PrismPdf.Tests;

/// <summary>
/// The COS escape hatch: read the object graph directly (§7.3), construct values, and commit an
/// edit either incrementally or as a full rewrite (§7.5).
/// </summary>
[TestFixture]
public sealed class CosJourneyTests : NativeTestBase
{
    private const string TwoPages = "valid/two-pages-text.pdf";

    /// <summary>The catalog is a dictionary, and it declares itself a <c>/Catalog</c>.</summary>
    [Test]
    public void CatalogObject_IsTheDocumentCatalog()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var catalog = doc.CatalogObject();

        Assert.That(catalog.Kind, Is.EqualTo(ObjectKind.Dictionary));
        Assert.That(catalog.DictionaryLength, Is.GreaterThan(0));

        using var type = catalog.DictionaryGet("Type");
        Assert.That(type, Is.Not.Null);
        Assert.That(type!.Kind, Is.EqualTo(ObjectKind.Name));
        Assert.That(type.Text, Is.EqualTo("Catalog"));
    }

    /// <summary>A missing key is absence — null — not an error (semantic contract 2).</summary>
    [Test]
    public void DictionaryGet_ReturnsNullForAMissingKey()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var catalog = doc.CatalogObject();

        Assert.That(catalog.DictionaryGet("NoSuchKeyExistsHere"), Is.Null);
    }

    /// <summary>A page dictionary carries its media box, with inherited attributes resolved.</summary>
    [Test]
    public void PageObject_CarriesTheResolvedPageDictionary()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var page = doc.PageObject(0);

        Assert.That(page.Kind, Is.EqualTo(ObjectKind.Dictionary));

        using var type = page.DictionaryGet("Type");
        Assert.That(type!.Text, Is.EqualTo("Page"));

        using var mediaBox = page.DictionaryGet("MediaBox");
        Assert.That(mediaBox, Is.Not.Null);
        Assert.That(mediaBox!.Kind, Is.EqualTo(ObjectKind.Array));
        Assert.That(mediaBox.ArrayLength, Is.EqualTo(4));

        using var width = mediaBox.ArrayGet(2);
        Assert.That(width.Kind, Is.AnyOf(ObjectKind.Integer, ObjectKind.Real));
    }

    /// <summary>An indirect reference resolves to the object it names.</summary>
    [Test]
    public void ResolveObject_FollowsAReference()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var catalog = doc.CatalogObject();
        using var pages = catalog.DictionaryGet("Pages");

        Assert.That(pages, Is.Not.Null);
        Assert.That(pages!.Kind, Is.EqualTo(ObjectKind.Reference));

        var reference = pages.Reference;
        Assert.That(reference.Number, Is.GreaterThan(0));
        Assert.That(reference.ToString(), Is.EqualTo($"{reference.Number} {reference.Generation} R"));

        using var resolved = doc.ResolveObject(pages);
        Assert.That(resolved.Kind, Is.EqualTo(ObjectKind.Dictionary));

        using var type = resolved.DictionaryGet("Type");
        Assert.That(type!.Text, Is.EqualTo("Pages"));

        using var fetched = doc.GetObject(reference);
        using var fetchedType = fetched.DictionaryGet("Type");
        Assert.That(fetchedType!.Text, Is.EqualTo("Pages"));
    }

    /// <summary>Resolving a direct object hands back a copy of it, not an error.</summary>
    [Test]
    public void ResolveObject_AcceptsADirectObject()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var direct = PdfObject.NewInteger(42);

        using var resolved = doc.ResolveObject(direct);

        Assert.That(resolved.Integer, Is.EqualTo(42));
    }

    /// <summary>A content stream's raw bytes come back still encoded.</summary>
    [Test]
    public void StreamRaw_LendsTheStillEncodedBytes()
    {
        using var doc = Document.Open(Corpus.Read("valid/flate-content.pdf"));
        using var page = doc.PageObject(0);
        using var contents = page.DictionaryGet("Contents");
        using var stream = doc.ResolveObject(contents!);

        Assert.That(stream.Kind, Is.EqualTo(ObjectKind.Stream));
        Assert.That(stream.StreamRaw, Is.Not.Empty);
        Assert.That(stream.DictionaryLength, Is.GreaterThan(0),
            "a stream exposes its dictionary through the same calls");

        using var filter = stream.DictionaryGet("Filter");
        Assert.That(filter!.Text, Is.EqualTo("FlateDecode"));
    }

    /// <summary>Every constructor produces an object of the kind it names.</summary>
    [Test]
    public void Constructors_ProduceEachKind()
    {
        using var nullObject = PdfObject.NewNull();
        using var boolean = PdfObject.NewBoolean(true);
        using var integer = PdfObject.NewInteger(-7);
        using var real = PdfObject.NewReal(1.5);
        using var text = PdfObject.NewString("hello");
        using var name = PdfObject.NewName("Type");
        using var reference = PdfObject.NewReference(new PdfReference(3, 0));
        using var array = PdfObject.NewArray();
        using var dictionary = PdfObject.NewDictionary();

        Assert.That(nullObject.Kind, Is.EqualTo(ObjectKind.Null));
        Assert.That(boolean.Kind, Is.EqualTo(ObjectKind.Boolean));
        Assert.That(boolean.Boolean, Is.True);
        Assert.That(integer.Kind, Is.EqualTo(ObjectKind.Integer));
        Assert.That(integer.Integer, Is.EqualTo(-7));
        Assert.That(real.Kind, Is.EqualTo(ObjectKind.Real));
        Assert.That(real.Real, Is.EqualTo(1.5));
        Assert.That(text.Kind, Is.EqualTo(ObjectKind.String));
        Assert.That(text.Text, Is.EqualTo("hello"));
        Assert.That(name.Kind, Is.EqualTo(ObjectKind.Name));
        Assert.That(name.Text, Is.EqualTo("Type"));
        Assert.That(reference.Reference, Is.EqualTo(new PdfReference(3, 0)));
        Assert.That(array.Kind, Is.EqualTo(ObjectKind.Array));
        Assert.That(array.ArrayLength, Is.Zero);
        Assert.That(dictionary.Kind, Is.EqualTo(ObjectKind.Dictionary));
        Assert.That(dictionary.DictionaryLength, Is.Zero);
    }

    /// <summary>An accessor for the wrong kind raises rather than coercing.</summary>
    [Test]
    public void TypedAccessors_DoNotCoerce()
    {
        using var integer = PdfObject.NewInteger(3);

        var ex = Assert.Throws<PrismPdfException>(() => _ = integer.Real);
        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.InvalidUse));
        Assert.Throws<PrismPdfException>(() => _ = integer.Boolean);
        Assert.Throws<PrismPdfException>(() => _ = integer.Bytes);
    }

    /// <summary>Containers take copies: mutating the source afterwards does not reach them.</summary>
    [Test]
    public void Containers_CloneWhatTheyAreGiven()
    {
        using var array = PdfObject.NewArray();
        using var dictionary = PdfObject.NewDictionary();

        using (var item = PdfObject.NewInteger(1))
        {
            array.ArrayPush(item);
            dictionary.DictionarySet("First", item);
        }

        Assert.That(array.ArrayLength, Is.EqualTo(1));
        Assert.That(dictionary.DictionaryLength, Is.EqualTo(1));

        using var stored = array.ArrayGet(0);
        Assert.That(stored.Integer, Is.EqualTo(1));

        using var fetched = dictionary.DictionaryGet("First");
        Assert.That(fetched!.Integer, Is.EqualTo(1));
    }

    /// <summary>A stream is a dictionary plus bytes, and both survive the round trip.</summary>
    [Test]
    public void NewStream_CarriesItsDictionaryAndBytes()
    {
        var payload = Encoding.ASCII.GetBytes("BT /F1 12 Tf ET");

        using var dictionary = PdfObject.NewDictionary();
        using (var length = PdfObject.NewInteger(payload.Length))
        {
            dictionary.DictionarySet("Length", length);
        }

        using var stream = PdfObject.NewStream(dictionary, payload);

        Assert.That(stream.Kind, Is.EqualTo(ObjectKind.Stream));
        Assert.That(stream.StreamRaw, Is.EqualTo(payload));
        Assert.That(stream.DictionaryLength, Is.EqualTo(1));
    }

    /// <summary>A clone is independent of its source.</summary>
    [Test]
    public void Clone_IsIndependent()
    {
        using var original = PdfObject.NewDictionary();
        using (var value = PdfObject.NewInteger(1))
        {
            original.DictionarySet("Key", value);
        }

        using var copy = original.Clone();

        using (var extra = PdfObject.NewInteger(2))
        {
            original.DictionarySet("Extra", extra);
        }

        Assert.That(original.DictionaryLength, Is.EqualTo(2));
        Assert.That(copy.DictionaryLength, Is.EqualTo(1), "the clone did not see the later change");
    }

    /// <summary>An incremental commit writes a document that still opens and still reads.</summary>
    [Test]
    public void Commit_Incremental_ProducesAReadableDocument()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        var source = Corpus.Read(TwoPages);
        var pages = doc.PageCount;
        var text = doc.PageText(0);

        using var catalog = doc.CatalogObject();
        using var pagesReference = catalog.DictionaryGet("Pages");
        var rootReference = RootReference(doc);

        using (var marker = PdfObject.NewName("Marked"))
        {
            catalog.DictionarySet("PrismPdfTestMarker", marker);
        }

        using var edit = new Edit(doc);
        edit.SetObject(rootReference, catalog);

        using var report = edit.Commit(EditCommitMode.Incremental);
        var updated = report.Bytes;

        Assert.That(edit.IsInvalid, Is.True, "a successful commit consumes the edit");
        Assert.That(updated.Length, Is.GreaterThan(source.Length),
            "an incremental commit appends rather than rewriting");

        using var reopened = Document.Open(updated);
        Assert.That(reopened.PageCount, Is.EqualTo(pages));
        Assert.That(reopened.PageText(0), Is.EqualTo(text));

        using var newCatalog = reopened.CatalogObject();
        using var marked = newCatalog.DictionaryGet("PrismPdfTestMarker");
        Assert.That(marked, Is.Not.Null);
        Assert.That(marked!.Text, Is.EqualTo("Marked"));
    }

    /// <summary>A full rewrite normalises the file; the report says what that cost.</summary>
    [Test]
    public void Commit_FullRewrite_ReportsWhatItDid()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        var rootReference = RootReference(doc);
        using var catalog = doc.CatalogObject();

        using (var marker = PdfObject.NewName("Rewritten"))
        {
            catalog.DictionarySet("PrismPdfTestMarker", marker);
        }

        using var edit = new Edit(doc);
        edit.SetObject(rootReference, catalog);

        using var report = edit.Commit(EditCommitMode.FullRewrite);

        Assert.That(report.RewriteMode, Is.EqualTo(RewriteMode.FullRewrite));

        using var reopened = Document.Open(report.Bytes);
        using var newCatalog = reopened.CatalogObject();
        using var marked = newCatalog.DictionaryGet("PrismPdfTestMarker");

        Assert.That(marked!.Text, Is.EqualTo("Rewritten"));
    }

    /// <summary>The source document is untouched by a commit — the boundary is immutable.</summary>
    [Test]
    public void Commit_LeavesTheSourceUntouched()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        var before = doc.PageText(0);
        var rootReference = RootReference(doc);

        using var catalog = doc.CatalogObject();
        using (var marker = PdfObject.NewInteger(1))
        {
            catalog.DictionarySet("PrismPdfTestMarker", marker);
        }

        using var edit = new Edit(doc);
        edit.SetObject(rootReference, catalog);
        using var report = edit.Commit();

        Assert.That(doc.PageText(0), Is.EqualTo(before));
        using var stillClean = doc.CatalogObject();
        Assert.That(stillClean.DictionaryGet("PrismPdfTestMarker"), Is.Null);
    }

    /// <summary>Null arguments are rejected in managed code, before any native call.</summary>
    [Test]
    public void NullArguments_AreRejected()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var edit = new Edit(doc);
        using var array = PdfObject.NewArray();

        Assert.Throws<ArgumentNullException>(() => new Edit(null!));
        Assert.Throws<ArgumentNullException>(() => edit.SetObject(new PdfReference(1, 0), null!));
        Assert.Throws<ArgumentNullException>(() => array.ArrayPush(null!));
        Assert.Throws<ArgumentNullException>(() => array.DictionarySet("Key", null!));
        Assert.Throws<ArgumentNullException>(() => doc.ResolveObject(null!));
        Assert.Throws<ArgumentNullException>(() => PdfObject.NewName((string)null!));
    }

    /// <summary>
    /// The catalog's own identity, found by scanning the trailer-adjacent objects the corpus files
    /// use. The ABI has no "which object is the root" call, so the reference is discovered by
    /// resolving candidates until one is the catalog.
    /// </summary>
    private static PdfReference RootReference(Document doc)
    {
        for (var number = 1; number <= 32; number++)
        {
            var candidate = new PdfReference(number, 0);
            using var value = doc.GetObject(candidate);
            if (value.Kind != ObjectKind.Dictionary)
            {
                continue;
            }

            using var type = value.DictionaryGet("Type");
            if (type is not null && type.Kind == ObjectKind.Name && type.Text == "Catalog")
            {
                return candidate;
            }
        }

        Assert.Fail("no catalog object found in the first 32 object numbers");
        return default;
    }
}
