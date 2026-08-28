namespace PrismPdf.Tests;

/// <summary>
/// The <c>manipulate.md</c> journey: merge, extract, rotate, round-trip, and read what each
/// transform cost through its report.
/// </summary>
[TestFixture]
public sealed class ManipulateJourneyTests : NativeTestBase
{
    private const string TwoPages = "valid/two-pages-text.pdf";

    /// <summary>Merging concatenates page counts, in order.</summary>
    [Test]
    public void Merge_ConcatenatesPages()
    {
        using var first = Document.Open(Corpus.Read(TwoPages));
        using var second = Document.Open(Corpus.Read("valid/minimal-2page.pdf"));
        var expected = first.PageCount + second.PageCount;

        using var merged = Document.Open(Pdf.Merge(first, second));

        Assert.That(merged.PageCount, Is.EqualTo(expected));
    }

    /// <summary>Extraction subsets, reorders and duplicates in one call.</summary>
    [Test]
    public void ExtractPages_SubsetsAndReorders()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        var lastPageText = doc.PageText(doc.PageCount - 1);

        using var extracted = Document.Open(doc.ExtractPages([doc.PageCount - 1, 0, 0]));

        Assert.That(extracted.PageCount, Is.EqualTo(3));
        Assert.That(extracted.PageText(0), Is.EqualTo(lastPageText));
    }

    /// <summary>Rotation produces a new document and leaves the source untouched.</summary>
    [Test]
    public void RotatePage_LeavesTheSourceUntouched()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        var before = doc.PageText(0);

        using var rotated = Document.Open(doc.RotatePage(0, 90));

        Assert.That(rotated.PageCount, Is.EqualTo(doc.PageCount));
        Assert.That(doc.PageText(0), Is.EqualTo(before), "the boundary is immutable");
    }

    /// <summary>All three save modes round-trip to the same page count and text.</summary>
    [Test]
    public void SaveModes_AllRoundTrip()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));
        var pages = doc.PageCount;
        var text = doc.PageText(0);

        foreach (var (name, bytes) in new[]
                 {
                     ("save", doc.Save()),
                     ("save_compact", doc.SaveCompact()),
                     ("save_packed", doc.SavePacked()),
                 })
        {
            using var reopened = Document.Open(bytes);
            Assert.That(reopened.PageCount, Is.EqualTo(pages), name);
            Assert.That(reopened.PageText(0), Is.EqualTo(text), name);
        }
    }

    /// <summary>
    /// The report variant carries the same bytes plus what the transform cost. A full rewrite of an
    /// unsigned document has nothing to invalidate, so the effects are the uninteresting case —
    /// which is precisely what makes it a good baseline assertion.
    /// </summary>
    [Test]
    public void SaveWithReport_ReportsRewriteMode()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        using var report = doc.SaveWithReport();

        Assert.That(report.RewriteMode, Is.EqualTo(RewriteMode.FullRewrite));
        Assert.That(report.Bytes, Is.Not.Empty);

        using var reopened = Document.Open(report.Bytes);
        Assert.That(reopened.PageCount, Is.EqualTo(doc.PageCount));
    }

    /// <summary>Saving at an exact version pins the header.</summary>
    [Test]
    public void SaveAs_PinsTheHeaderVersion()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        using var reopened = Document.Open(doc.SaveAs(1, 7));

        Assert.That(reopened.Version, Is.EqualTo(new PdfVersion(1, 7)));
    }
}
