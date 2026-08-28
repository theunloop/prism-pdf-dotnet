namespace PrismPdf.Tests;

/// <summary>
/// The vertical slice from the binding author's guide, in order.
/// </summary>
/// <remarks>
/// <para>
/// "Every new binding starts with the same slice, in this order, because it exercises every
/// convention once (status codes, last-error, owned handle, owned string, owned bytes, failure
/// path)."
/// </para>
/// <para>
/// If these pass, the six semantic contracts are wired correctly and every other area is a matter
/// of adding surface, not of re-deciding conventions. If one of these fails, nothing else in the
/// suite is trustworthy — which is why they live in their own fixture and run first.
/// </para>
/// </remarks>
[TestFixture]
[Order(0)]
public sealed class VerticalSliceTests : NativeTestBase
{
    private const string SlicePdf = "valid/two-pages-text.pdf";

    /// <summary>Step 1: a static string that is never freed.</summary>
    [Test]
    public void Version_ReportsTheEngineVersion()
    {
        // The release workflow refuses to build a tag whose workspace version disagrees, so the
        // string the engine reports is the tag this binding is pinned to. See native/VENDORED.md.
        Assert.That(Pdf.Version, Is.EqualTo("0.4.0"));
    }

    /// <summary>Step 2: an owned handle whose disposal frees it.</summary>
    [Test]
    public void Open_ValidDocument_Succeeds()
    {
        using var doc = Document.Open(Corpus.Read(SlicePdf));

        Assert.That(doc.IsInvalid, Is.False);
    }

    /// <summary>Step 3: the parse failure path, with a diagnostic message.</summary>
    [Test]
    public void Open_Garbage_ThrowsParse()
    {
        var garbage = "this is definitively not a PDF"u8.ToArray();

        var ex = Assert.Throws<PrismPdfException>(() => Document.Open(garbage));

        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.Parse));
        Assert.That(ex.Message, Does.Contain("Parse"));
    }

    /// <summary>Step 4: the password failure path — a distinct status, not a generic parse error.</summary>
    [Test]
    public void Open_EncryptedWithWrongPassword_ThrowsPassword()
    {
        using var source = Document.Open(Corpus.Read(SlicePdf));
        var encrypted = source.SaveEncrypted("correct-password");

        var ex = Assert.Throws<PrismPdfException>(() => Document.Open(encrypted, "wrong-password"));

        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.Password));
    }

    /// <summary>Step 5: a plain value out-param.</summary>
    [Test]
    public void PageCount_ReadsAPlainOutParam()
    {
        using var doc = Document.Open(Corpus.Read(SlicePdf));

        Assert.That(doc.PageCount, Is.EqualTo(2));
    }

    /// <summary>Step 6: an owned string, copied then freed.</summary>
    [Test]
    public void PageText_ReturnsAnOwnedString()
    {
        using var doc = Document.Open(Corpus.Read(SlicePdf));

        var text = doc.PageText(0);

        Assert.That(text, Is.Not.Empty);
    }

    /// <summary>
    /// Step 7: NotFound from an <em>index</em> lookup is an error, not absence. This is the one
    /// place the two readings of NotFound have to be told apart, so it gets its own test.
    /// </summary>
    [Test]
    public void PageText_PastTheLastPage_ThrowsNotFound()
    {
        using var doc = Document.Open(Corpus.Read(SlicePdf));

        var ex = Assert.Throws<PrismPdfException>(() => doc.PageText(doc.PageCount + 1));

        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.NotFound));
    }

    /// <summary>Step 8: an owned byte buffer, copied then freed with its original length.</summary>
    [Test]
    public void Save_ReturnsAnOwnedBuffer()
    {
        using var doc = Document.Open(Corpus.Read(SlicePdf));

        var bytes = doc.Save();

        Assert.That(bytes, Is.Not.Empty);
        Assert.That(bytes[..5], Is.EqualTo("%PDF-"u8.ToArray()));
    }

    /// <summary>
    /// Step 9: the round trip through the binding itself. Asserting through the SDK's own read API
    /// beats a golden-byte comparison — it is what a consumer would actually do.
    /// </summary>
    [Test]
    public void Save_RoundTripsThroughTheBinding()
    {
        using var original = Document.Open(Corpus.Read(SlicePdf));
        var expectedPages = original.PageCount;
        var expectedText = original.PageText(0);

        using var reopened = Document.Open(original.Save());

        Assert.That(reopened.PageCount, Is.EqualTo(expectedPages));
        Assert.That(reopened.PageText(0), Is.EqualTo(expectedText));
    }
}
