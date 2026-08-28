namespace PrismPdf.Tests;

/// <summary>
/// The layout half of the <c>compose.md</c> journey: pour content into a <see cref="Flow"/>, let
/// it break pages, and assert on the result by reopening it.
/// </summary>
[TestFixture]
public sealed class ComposeJourneyTests : NativeTestBase
{
    /// <summary>A paragraph poured into a flow comes back out of the built document.</summary>
    [Test]
    public void Flow_PoursTextIntoAPage()
    {
        using var flow = NewFlow();
        using var body = Body();
        flow.AddText(body, "A first paragraph.");

        using var doc = Document.Open(flow.Build());

        Assert.That(doc.PageCount, Is.EqualTo(1));
        Assert.That(doc.PageText(0), Does.Contain("first paragraph"));
    }

    /// <summary>The cursor descends as content is poured, and a page break resets it.</summary>
    [Test]
    public void Flow_TracksItsCursorAndPageCount()
    {
        using var flow = NewFlow();
        using var body = Body();

        var start = flow.CursorY;
        flow.AddText(body, "A paragraph that moves the cursor down the page.");
        var afterText = flow.CursorY;

        Assert.That(afterText, Is.LessThan(start));
        Assert.That(flow.PageCount, Is.EqualTo(1));

        flow.PageBreak();

        Assert.That(flow.PageCount, Is.EqualTo(2));
        Assert.That(flow.CursorY, Is.EqualTo(start), "a new page starts the cursor at the top again");

        flow.AddText(body, "On the second page.");
        Assert.That(flow.CursorY, Is.EqualTo(afterText));
    }

    /// <summary>Enough content breaks pages on its own.</summary>
    [Test]
    public void Flow_BreaksPagesWhenContentOverflows()
    {
        using var flow = NewFlow();
        using var body = Body();
        for (var i = 0; i < 120; i++)
        {
            flow.AddText(body, $"Paragraph {i}, long enough to occupy a line of the text column.");
        }

        using var doc = Document.Open(flow.Build());

        Assert.That(doc.PageCount, Is.GreaterThan(1));
    }

    /// <summary>Explicit vertical space advances the cursor without drawing.</summary>
    [Test]
    public void Flow_AddSpaceMovesTheCursor()
    {
        using var flow = NewFlow();
        var before = flow.CursorY;

        flow.AddSpace(50);

        Assert.That(flow.CursorY, Is.EqualTo(before - 50).Within(0.001));
    }

    /// <summary>Headings, lists, tables and running furniture all reach the document.</summary>
    [Test]
    public void Flow_PoursEveryBlockKind()
    {
        using var flow = NewFlow();
        using var body = Body();
        using var heading = new TextBlock("F1", "Helvetica-Bold", 18, 22);

        flow.SetHeader(body, "A running header");
        flow.SetFooter(body, "A running footer");
        flow.AddTitleElement(heading, "The title");
        flow.AddHeading(1, heading, "A heading");
        flow.AddText(body, "A paragraph.");
        flow.AddList(body, ["first", "second"], ListStyle.Numbered);
        flow.AddBookmark("The heading");

        using var table = new Table([120.0, 120.0]) { Border = 0.5, Padding = 4, HeaderRow = true };
        table.SetFont("F1", "Helvetica");
        table.AddRow("Item", "Cost");
        table.AddRow("Widget", "3.00");
        flow.AddTable(table);

        using var doc = Document.Open(flow.Build());
        var text = doc.PageText(0);

        Assert.That(text, Does.Contain("The title"));
        Assert.That(text, Does.Contain("A heading"));
        Assert.That(text, Does.Contain("first"));
        Assert.That(text, Does.Contain("Widget"));
        Assert.That(text, Does.Contain("A running header"));

        using var outline = doc.Outline();
        Assert.That(outline.Count, Is.EqualTo(1));
    }

    /// <summary>Images reach the page, both as artifacts and as tagged figures.</summary>
    [Test]
    public void Flow_PlacesImagesAndFigures()
    {
        using var image = ImageSource.FromRgb(2, 2, [0xFF, 0, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0]);
        using var flow = NewFlow();
        using var body = Body();
        flow.SetTagged("en-GB");

        flow.AddImage(image, 40, 40);
        flow.AddImageFit(image, 60);
        flow.AddFigure(image, 40, 40, "A four-pixel swatch");
        flow.AddFigureFit(image, 60, "The same swatch, scaled");
        flow.AddFigureWithCaption(image, 40, 40, "The swatch again", body, "Figure 1: a swatch");

        using var doc = Document.Open(flow.Build());
        using var images = doc.PageImages(0);

        Assert.That(images.Count, Is.EqualTo(5));
        Assert.That(doc.PageText(0), Does.Contain("Figure 1"));
    }

    /// <summary>Tagging turns on the structure tree, which the accessibility elements need.</summary>
    [Test]
    public void Flow_TaggedEmitsAccessibilityElements()
    {
        using var flow = NewFlow();
        using var body = Body();
        flow.SetTagged("en-GB");
        flow.Title = "Tagged";
        flow.Author = "PrismPdf.Tests";
        flow.SetInfo("Custom", "value");

        flow.AddText(body, "A paragraph with a footnote.");
        flow.AddNote(body, "The footnote itself.", "fn1");
        flow.AddFormula(body, "E = mc²", "E equals m c squared");

        using var doc = Document.Open(flow.Build());

        Assert.That(doc.Info("Title"), Is.EqualTo("Tagged"));
        Assert.That(doc.Info("Author"), Is.EqualTo("PrismPdf.Tests"));
        Assert.That(doc.Info("Custom"), Is.EqualTo("value"));
        Assert.That(doc.PageText(0), Does.Contain("footnote"));
    }

    /// <summary>The PDF/UA-2 footnote form carries its citations.</summary>
    [Test]
    public void Flow_FeNoteCarriesCitations()
    {
        using var flow = NewFlow();
        using var body = Body();
        flow.SetTagged("en-GB");
        flow.AddText(body, "A claim.");
        flow.AddFeNote(body, "The supporting note.", "fn1", ["cite1"]);

        using var doc = Document.Open(flow.Build());

        Assert.That(doc.PageText(0), Does.Contain("supporting note"));
    }

    /// <summary>
    /// <c>IntoBuilder</c> is the composition point: the flow's pages arrive in a builder that can
    /// still be post-processed.
    /// </summary>
    [Test]
    public void IntoBuilder_HandsThePagesToTheBuilder()
    {
        var flow = NewFlow();
        using var body = Body();
        flow.AddText(body, "Poured, then post-processed.");

        using var builder = flow.IntoBuilder();
        Assert.That(flow.IsInvalid, Is.True, "into_builder consumes the flow");

        builder.Title = "Post-processed";
        builder.AddOutline("Start", 0);

        using var doc = Document.Open(builder.Build());

        Assert.That(doc.Info("Title"), Is.EqualTo("Post-processed"));
        Assert.That(doc.PageText(0), Does.Contain("post-processed"));
    }

    /// <summary>Building consumes the flow; everything afterwards raises rather than double-frees.</summary>
    [Test]
    public void Build_ConsumesTheFlow()
    {
        var flow = NewFlow();
        using var body = Body();
        flow.AddText(body, "Once.");

        _ = flow.Build();

        Assert.That(flow.IsInvalid, Is.True);
        Assert.Throws<ObjectDisposedException>(() => flow.Build());
        Assert.Throws<ObjectDisposedException>(() => _ = flow.PageCount);
        Assert.DoesNotThrow(flow.Dispose);
    }

    /// <summary>A style measures and wraps with the same metrics the flow lays out with.</summary>
    [Test]
    public void TextBlock_MeasuresAndWraps()
    {
        using var body = Body();

        var width = body.MeasureText("Hello");
        Assert.That(width, Is.GreaterThan(0));
        Assert.That(body.MeasureText("Hello Hello"), Is.GreaterThan(width));

        using var lines = body.WrapText(
            "A sentence with enough words in it that it cannot fit on one short line.", 80);

        Assert.That(lines.Count, Is.GreaterThan(1));
        Assert.That(lines[0], Is.Not.Empty);
    }

    /// <summary>An embedded font replaces the Standard-14 font of the same resource name.</summary>
    [Test]
    public void EmbedFont_RejectsSomethingThatIsNotAnSfnt()
    {
        using var flow = NewFlow();

        var ex = Assert.Throws<PrismPdfException>(() => flow.EmbedFont("F1", [0x00, 0x01, 0x02]));
        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.Parse));
        Assert.That(flow.IsInvalid, Is.False, "a rejected call leaves the flow usable");
    }

    /// <summary>Null arguments are rejected in managed code, before any native call.</summary>
    [Test]
    public void NullArguments_AreRejected()
    {
        using var flow = NewFlow();
        using var body = Body();

        Assert.Throws<ArgumentNullException>(() => flow.AddText(null!, "text"));
        Assert.Throws<ArgumentNullException>(() => flow.AddText(body, null!));
        Assert.Throws<ArgumentNullException>(() => flow.AddTable(null!));
        Assert.Throws<ArgumentNullException>(() => flow.AddImage(null!, 10, 10));
        Assert.Throws<ArgumentNullException>(() => flow.Title = null!);
        Assert.Throws<ArgumentNullException>(() => new TextBlock(null!, "Helvetica", 10, 12));
        Assert.Throws<ArgumentNullException>(() => body.MeasureText(null!));
    }

    private static Flow NewFlow() => new(
        PdfSize.A4,
        PdfMargins.Uniform(72),
        new Dictionary<string, StdFont> { ["F1"] = StdFont.Helvetica });

    private static TextBlock Body() => new("F1", "Helvetica", 11, 14);
}
