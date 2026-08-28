namespace PrismPdf.Tests;

/// <summary>
/// The <c>parse.md</c> journey: open files from the shared corpus, assert page counts, versions and
/// extracted text, and prove that malformed input opens via recovery or fails cleanly — never
/// crashes.
/// </summary>
[TestFixture]
public sealed class ParseJourneyTests : NativeTestBase
{
    /// <summary>Every well-formed corpus file opens and reports a page count.</summary>
    [TestCaseSource(nameof(ValidFiles))]
    public void ValidCorpus_OpensStrictly(string path)
    {
        using var doc = Document.Open(File.ReadAllBytes(path));

        Assert.That(doc.PageCount, Is.GreaterThan(0), path);

        using var report = doc.GetOpenReport();
        Assert.That(report.Mode, Is.EqualTo(OpenMode.Strict), $"{path} should not need recovery");
    }

    /// <summary>
    /// Every malformed corpus file either opens or fails with <see cref="PrismPdfStatus.Parse"/>.
    /// What it must never do is crash the process — which is exactly what this test is here to
    /// catch.
    /// </summary>
    /// <remarks>
    /// Opening is not the same as <see cref="OpenMode.Recovered"/>. The engine sets that mode only
    /// when the cross-reference itself had to be rebuilt — <c>XrefParseFailure</c> or
    /// <c>UnreachableCatalog</c>. Leniency below the xref layer does not, and two files here rely
    /// on exactly that: <c>wrong-length.pdf</c> has an intact xref and a short <c>/Length</c> the
    /// parser resolves by scanning to <c>endstream</c>, and <c>garbage-prefix.pdf</c> has junk
    /// before <c>%PDF</c> but absolute xref offsets that still resolve. Both open strictly, and
    /// that is correct. So the diagnostics assertion below is scoped to a recovered open.
    /// </remarks>
    [TestCaseSource(nameof(MalformedFiles))]
    public void MalformedCorpus_RecoversOrFailsCleanly(string path)
    {
        Document? doc = null;
        try
        {
            doc = Document.Open(File.ReadAllBytes(path));
        }
        catch (PrismPdfException ex)
        {
            Assert.That(ex.Status, Is.EqualTo(PrismPdfStatus.Parse), path);
            return;
        }

        using (doc)
        {
            Assert.That(doc.PageCount, Is.GreaterThanOrEqualTo(0), path);

            using var report = doc.GetOpenReport();
            if (report.Mode is OpenMode.Recovered)
            {
                Assert.That(report.Diagnostics(), Is.Not.Empty,
                    $"{path} reports having been recovered, so it must say why");
            }
        }
    }

    /// <summary>
    /// The corpus files whose cross-reference is genuinely broken open through recovery, and say
    /// why they had to.
    /// </summary>
    /// <remarks>
    /// The sibling test above is deliberately permissive about the mode, so this is the one that
    /// holds recovery itself to account: without it the suite would still pass if the engine
    /// stopped recovering and simply refused these files.
    /// </remarks>
    [TestCase("malformed/missing-startxref.pdf")]
    [TestCase("malformed/bad-startxref.pdf")]
    [TestCase("malformed/truncated-trailer.pdf")]
    public void BrokenXref_OpensRecoveredAndReportsWhy(string relativePath)
    {
        using var doc = Document.Open(Corpus.Read(relativePath));

        using var report = doc.GetOpenReport();

        Assert.That(report.Mode, Is.EqualTo(OpenMode.Recovered), relativePath);
        Assert.That(report.Diagnostics(), Is.Not.Empty,
            "a recovered open should say why it recovered");
    }

    /// <summary>Legal-but-unusual files open and round-trip like any other.</summary>
    [TestCaseSource(nameof(EdgeFiles))]
    public void EdgeCorpus_OpensAndRoundTrips(string path)
    {
        using var doc = Document.Open(File.ReadAllBytes(path));
        var pages = doc.PageCount;

        using var reopened = Document.Open(doc.Save());

        Assert.That(reopened.PageCount, Is.EqualTo(pages), path);
    }

    /// <summary>The header version is reported as declared.</summary>
    [Test]
    public void Version_IsRead()
    {
        using var doc = Document.Open(Corpus.Read("valid/two-pages-text.pdf"));

        Assert.That(doc.Version, Is.Not.Null);
        Assert.That(doc.Version!.Value.Major, Is.EqualTo(1));
    }

    /// <summary>Whole-document extraction concatenates the pages' text.</summary>
    [Test]
    public void Text_CoversEveryPage()
    {
        using var doc = Document.Open(Corpus.Read("valid/two-pages-text.pdf"));

        var whole = doc.Text;

        for (var page = 0; page < doc.PageCount; page++)
        {
            var pageText = doc.PageText(page).Trim();
            if (pageText.Length > 0)
            {
                Assert.That(whole, Does.Contain(pageText));
            }
        }
    }

    /// <summary>Anti-DoS limits are honoured, and a limit that bites is a clean refusal.</summary>
    [Test]
    public void OpenOptions_ApplyLimits()
    {
        using var options = new OpenOptions { MaxDepth = 1 };

        // Either the document parses within the tightened bound or it is refused. Both are
        // correct; a crash or a hang is not, and that is what this asserts.
        try
        {
            using var doc = Document.Open(Corpus.Read("valid/objstm.pdf"), options);
            Assert.That(doc.PageCount, Is.GreaterThanOrEqualTo(0));
        }
        catch (PrismPdfException ex)
        {
            Assert.That(ex.Status, Is.EqualTo(PrismPdfStatus.Parse));
        }
    }

    private static IEnumerable<string> ValidFiles() => Corpus.Files("valid");

    private static IEnumerable<string> MalformedFiles() => Corpus.Files("malformed");

    private static IEnumerable<string> EdgeFiles() => Corpus.Files("edge");
}
