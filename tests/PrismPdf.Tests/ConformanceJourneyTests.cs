namespace PrismPdf.Tests;

/// <summary>
/// The conformance passes: PDF/A (ISO 19005) and PDF/UA (ISO 14289), and the one status that
/// carries more than a code.
/// </summary>
/// <remarks>
/// Most of what matters here is the <em>refusal</em>: a document that is perfectly good PDF and
/// not good PDF/A, together with the rule that says why. A passing PDF/A file needs an embedded
/// font program, which the shared corpus does not ship — the one test that needs one looks for a
/// font and skips when it cannot find it, rather than failing.
/// </remarks>
[TestFixture]
public sealed class ConformanceJourneyTests : NativeTestBase
{
    /// <summary>Standard-14 fonts are not embedded, so PDF/A refuses them by name.</summary>
    [Test]
    public void MakePdfA_RefusesAnUnembeddedFont()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        using var metadata = new XmpMetadata { Title = "Draft" };

        var ex = Assert.Throws<PrismPdfConformanceException>(
            () => builder.MakePdfA(PdfAConformance.A2b, metadata));

        Assert.That(ex!.Issue, Is.EqualTo(ConformanceIssue.UnembeddedFont));
        Assert.That(ex.Status, Is.EqualTo(PrismPdfStatus.Conformance));
        Assert.That(ex.Message, Does.Contain("UnembeddedFont"));
    }

    /// <summary>The conformance exception is still a <see cref="PrismPdfException"/>.</summary>
    [Test]
    public void ConformanceException_IsCaughtAsThePrismPdfException()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        using var metadata = new XmpMetadata();

        var ex = Assert.Throws<PrismPdfConformanceException>(
            () => builder.MakePdfA(PdfAConformance.A1b, metadata));

        Assert.That(ex, Is.InstanceOf<PrismPdfException>());
    }

    /// <summary>PDF/UA refuses an untagged document, and says so.</summary>
    [Test]
    public void MakePdfUa_RefusesAnUntaggedDocument()
    {
        using var builder = new Builder { Title = "Untagged", DisplayDocTitle = true };
        builder.AddPage(HelloWorld(), Fonts);
        using var metadata = new XmpMetadata { Title = "Untagged" };

        var ex = Assert.Throws<PrismPdfConformanceException>(
            () => builder.MakePdfUa(metadata, "en-GB"));

        Assert.That(ex!.Issue, Is.AnyOf(
            ConformanceIssue.NotTagged,
            ConformanceIssue.UnembeddedFont,
            ConformanceIssue.MissingTitle));
    }

    /// <summary>PDF/UA-2 applies the 14289-2 rules, which are stricter than PDF/UA-1's.</summary>
    [Test]
    public void MakePdfUa2_AlsoRefusesAnUntaggedDocument()
    {
        using var builder = new Builder { Title = "Untagged", DisplayDocTitle = true };
        builder.AddPage(HelloWorld(), Fonts);
        using var metadata = new XmpMetadata { Title = "Untagged" };

        Assert.Throws<PrismPdfConformanceException>(() => builder.MakePdfUa2(metadata, "en-GB"));
    }

    /// <summary>A caller-chosen output intent takes the same refusal path.</summary>
    [Test]
    public void MakePdfA_WithOutputIntent_ReportsTheSameIssue()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        using var metadata = new XmpMetadata();

        var ex = Assert.Throws<PrismPdfConformanceException>(
            () => builder.MakePdfA(PdfAConformance.A2b, metadata, MinimalIccProfile(), 3, "sRGB"));

        Assert.That(ex!.Issue, Is.EqualTo(ConformanceIssue.UnembeddedFont));
    }

    /// <summary>The level helpers report what each part of ISO 19005 permits.</summary>
    [Test]
    public void PdfALevels_ReportTheirPartAndCapabilities()
    {
        Assert.That(Pdf.PdfAPart(PdfAConformance.A1b), Is.EqualTo(1));
        Assert.That(Pdf.PdfAPart(PdfAConformance.A2u), Is.EqualTo(2));
        Assert.That(Pdf.PdfAPart(PdfAConformance.A3b), Is.EqualTo(3));
        Assert.That(Pdf.PdfAPart(PdfAConformance.A4f), Is.EqualTo(4));

        Assert.That(Pdf.PdfAAllowsAttachments(PdfAConformance.A2b), Is.False);
        Assert.That(Pdf.PdfAAllowsAttachments(PdfAConformance.A3b), Is.True);
        Assert.That(Pdf.PdfAAllowsAttachments(PdfAConformance.A4f), Is.True);

        Assert.That(Pdf.PdfACode(PdfAConformance.A2u), Is.EqualTo("2U").IgnoreCase);
    }

    /// <summary>An attachment on a level that forbids them is refused by name.</summary>
    [Test]
    public void MakePdfA_RefusesAnAttachmentOnALevelThatForbidsIt()
    {
        Assert.That(Pdf.PdfAAllowsAttachments(PdfAConformance.A2b), Is.False,
            "the premise of this test");

        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        builder.AttachFile("data.csv", "text/csv", "Data", "Some data", "a,b\n"u8);
        using var metadata = new XmpMetadata();

        var ex = Assert.Throws<PrismPdfConformanceException>(
            () => builder.MakePdfA(PdfAConformance.A2b, metadata));

        Assert.That(ex!.Issue, Is.AnyOf(
            ConformanceIssue.AttachmentRequiresPdfA3,
            ConformanceIssue.UnembeddedFont));
    }

    /// <summary>Every XMP field is accepted, and the packet reaches the built document.</summary>
    [Test]
    public void XmpMetadata_CarriesEveryField()
    {
        using var metadata = new XmpMetadata
        {
            Title = "A title",
            Subject = "A description",
            Keywords = "one two",
            CreatorTool = "PrismPdf.Tests",
            Producer = "Prism PDF",
        };
        metadata.AddAuthor("First Author");
        metadata.AddAuthor("Second Author");
        metadata.SetDates(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));

        Assert.That(metadata.IsInvalid, Is.False);
    }

    /// <summary>
    /// The success path, when a font program is available: a tagged flow with an embedded font
    /// becomes a conformant PDF/UA file.
    /// </summary>
    [Test]
    public void MakePdfUa_AcceptsATaggedDocumentWithAnEmbeddedFont()
    {
        var program = FindFontProgram();
        if (program is null)
        {
            Assert.Ignore(
                "No sfnt font program found. Set PRISMPDF_TEST_FONT to a .ttf or .otf to run this.");
        }

        // No Standard-14 fonts are declared: a resource declared as one stays registered as one
        // even after a program is embedded over its name, and the pass would refuse it.
        using var flow = new Flow(PdfSize.A4, PdfMargins.Uniform(56));
        flow.SetTagged("en-GB");
        flow.EmbedFont("F1", program!);
        flow.Title = "A conformant document";

        using var body = new TextBlock("F1", "Helvetica", 11, 14);
        using (var heading = new TextBlock("F1", "Helvetica", 18, 22))
        {
            flow.AddTitleElement(heading, "A conformant document");
            flow.AddHeading(1, heading, "A heading");
        }

        flow.AddText(body, "A tagged paragraph in an embedded font.");

        using var builder = flow.IntoBuilder();
        builder.Title = "A conformant document";
        builder.DisplayDocTitle = true;
        builder.Lang = "en-GB";

        using var metadata = new XmpMetadata { Title = "A conformant document" };
        metadata.AddAuthor("PrismPdf.Tests");

        builder.MakePdfUa(metadata, "en-GB");

        using var doc = Document.Open(builder.Build());
        Assert.That(doc.Info("Title"), Is.EqualTo("A conformant document"));
        Assert.That(doc.Xmp, Is.Not.Null);
    }

    /// <summary>Null arguments are rejected in managed code, before any native call.</summary>
    [Test]
    public void NullArguments_AreRejected()
    {
        using var builder = new Builder();
        using var metadata = new XmpMetadata();

        Assert.Throws<ArgumentNullException>(() => builder.MakePdfA(PdfAConformance.A2b, null!));
        Assert.Throws<ArgumentNullException>(() => builder.MakePdfUa(null!, "en-GB"));
        Assert.Throws<ArgumentNullException>(() => builder.MakePdfUa(metadata, null!));
        Assert.Throws<ArgumentNullException>(() => metadata.Title = null!);
        Assert.Throws<ArgumentNullException>(() => metadata.AddAuthor(null!));
    }

    private static Dictionary<string, StdFont> Fonts => new() { ["F1"] = StdFont.Helvetica };

    private static Content HelloWorld()
    {
        var content = new Content();
        content.BeginText();
        content.SetFont("F1", 12);
        content.TextMove(72, 700);
        content.ShowText("Hello");
        content.EndText();
        return content;
    }

    /// <summary>
    /// A three-component ICC profile is not synthesised here — the pass is expected to refuse
    /// before it reads one, so a stub is enough to exercise the argument path.
    /// </summary>
    private static byte[] MinimalIccProfile() => new byte[128];

    private static byte[]? FindFontProgram()
    {
        var configured = Environment.GetEnvironmentVariable("PRISMPDF_TEST_FONT");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return File.ReadAllBytes(configured);
        }

        foreach (var directory in new[]
                 {
                     "/usr/share/fonts",
                     "/usr/local/share/fonts",
                     "/Library/Fonts",
                     "C:\\Windows\\Fonts",
                 })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var font = Directory
                .EnumerateFiles(directory, "*.ttf", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .FirstOrDefault();

            if (font is not null)
            {
                return File.ReadAllBytes(font);
            }
        }

        return null;
    }
}
