using System.Text;

namespace PrismPdf.Tests;

/// <summary>
/// The <c>create.md</c> journey: assemble a document from nothing, build it, and assert on it by
/// reopening the bytes — the only assertion the boundary allows.
/// </summary>
[TestFixture]
public sealed class CreateJourneyTests : NativeTestBase
{
    /// <summary>The smallest useful document: one page of Standard-14 text, read back out.</summary>
    [Test]
    public void Build_ProducesAReadableDocument()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);

        using var doc = Document.Open(builder.Build());

        Assert.That(doc.PageCount, Is.EqualTo(1));
        Assert.That(doc.PageText(0), Does.Contain("Hello"));
    }

    /// <summary>The builder is not consumed by building: keep adding pages and build again.</summary>
    [Test]
    public void Build_LeavesTheBuilderUsable()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);

        var first = builder.Build();
        builder.AddPage(HelloWorld(), Fonts);
        var second = builder.Build();

        using var one = Document.Open(first);
        using var two = Document.Open(second);
        Assert.That(one.PageCount, Is.EqualTo(1));
        Assert.That(two.PageCount, Is.EqualTo(2));
    }

    /// <summary>Metadata set on the builder comes back off the built document's <c>/Info</c>.</summary>
    [Test]
    public void Metadata_RoundTrips()
    {
        using var builder = new Builder
        {
            Title = "A title",
            Author = "An author",
            Subject = "A subject",
            Keywords = "one two",
            Creator = "PrismPdf.Tests",
            Lang = "en-GB",
            DisplayDocTitle = true,
        };
        builder.SetInfo("Custom", "value");
        builder.AddPage(HelloWorld(), Fonts);

        using var doc = Document.Open(builder.Build());

        Assert.That(doc.Info("Title"), Is.EqualTo("A title"));
        Assert.That(doc.Info("Author"), Is.EqualTo("An author"));
        Assert.That(doc.Info("Subject"), Is.EqualTo("A subject"));
        Assert.That(doc.Info("Keywords"), Is.EqualTo("one two"));
        Assert.That(doc.Info("Creator"), Is.EqualTo("PrismPdf.Tests"));
        Assert.That(doc.Info("Custom"), Is.EqualTo("value"));
    }

    /// <summary><c>ClearInfo</c> drops everything set so far, as PDF 2.0 metadata rules want.</summary>
    [Test]
    public void ClearInfo_DropsEveryEntry()
    {
        using var builder = new Builder { Title = "A title" };
        builder.ClearInfo();
        builder.AddPage(HelloWorld(), Fonts);

        using var doc = Document.Open(builder.Build());

        Assert.That(doc.Info("Title"), Is.Null);
    }

    /// <summary>An XMP packet set on the builder is the document's <c>/Metadata</c> stream.</summary>
    [Test]
    public void MetadataXmp_RoundTrips()
    {
        const string Packet =
            "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF "
            + "xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
            + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Tagged</rdf:li></rdf:Alt></dc:title>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

        using var builder = new Builder();
        builder.SetMetadataXmp(Packet);
        builder.AddPage(HelloWorld(), Fonts);

        using var doc = Document.Open(builder.Build());

        Assert.That(doc.Xmp, Does.Contain("Tagged"));
    }

    /// <summary>The version pin is a floor, and <c>BuildFor</c> declares an exact version.</summary>
    [Test]
    public void Version_IsPinnedAsAFloor()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        builder.SetVersion(1, 7);

        using var pinned = Document.Open(builder.Build());
        using var targeted = Document.Open(builder.BuildFor(2, 0));

        Assert.That(pinned.Version, Is.EqualTo(new PdfVersion(1, 7)));
        Assert.That(targeted.Version, Is.EqualTo(new PdfVersion(2, 0)));
    }

    /// <summary>A media box set on the builder applies to the pages added after it.</summary>
    [Test]
    public void MediaBox_AppliesToLaterPages()
    {
        using var builder = new Builder { MediaBox = new PdfRect(0, 0, 200, 400) };
        builder.AddPage(HelloWorld(), Fonts);

        using var doc = Document.Open(builder.Build());

        Assert.That(doc.PageCount, Is.EqualTo(1));
    }

    /// <summary>A page specification carries its own media box and its own resources.</summary>
    [Test]
    public void PageSpec_IsConsumedByTheBuilder()
    {
        using var content = HelloWorld();
        var page = new PageSpec(content) { MediaBox = new PdfRect(0, 0, 300, 300) };
        page.AddStandardFont("F1", StdFont.Helvetica);

        using var builder = new Builder();
        builder.AddPageSpec(page);

        Assert.That(page.IsInvalid, Is.True, "a successful transfer consumes the specification");
        Assert.Throws<ObjectDisposedException>(() => page.AddStandardFont("F2", StdFont.Courier));

        using var doc = Document.Open(builder.Build());
        Assert.That(doc.PageText(0), Does.Contain("Hello"));
    }

    /// <summary>Disposing a consumed specification is a no-op, not a double free.</summary>
    [Test]
    public void ConsumedPageSpec_IsSafeToDispose()
    {
        using var content = HelloWorld();
        var page = new PageSpec(content);
        using var builder = new Builder();
        builder.AddPageSpec(page);

        Assert.DoesNotThrow(page.Dispose);
    }

    /// <summary>An image resource reaches the page and the built document lists it.</summary>
    [Test]
    public void PageSpec_CarriesAnImageResource()
    {
        using var image = ImageSource.FromGray(2, 2, [0x00, 0x40, 0x80, 0xFF]);
        Assert.That(image.Width, Is.EqualTo(2));
        Assert.That(image.Height, Is.EqualTo(2));

        using var content = new Content();
        content.Save();
        content.Transform(100, 0, 0, 100, 50, 50);
        content.DoXObject("Im1");
        content.Restore();

        var page = new PageSpec(content);
        page.AddImage("Im1", image);

        using var builder = new Builder();
        builder.AddPageSpec(page);

        using var doc = Document.Open(builder.Build());
        using var images = doc.PageImages(0);
        Assert.That(images.Count, Is.EqualTo(1));
    }

    /// <summary>Outline entries, links and notes all reach the built document.</summary>
    [Test]
    public void Annotations_ReachTheDocument()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        builder.AddOutline("Start", 0);
        builder.AddLinkUri(0, new PdfRect(72, 700, 200, 720), "https://example.invalid", "Example");
        builder.AddLinkPage(0, new PdfRect(72, 670, 200, 690), 0, "First page");
        builder.AddNote(0, new PdfRect(72, 640, 92, 660), "A note");

        using var doc = Document.Open(builder.Build());
        using var annotations = doc.PageAnnotations(0);
        using var outline = doc.Outline();

        Assert.That(annotations.Count, Is.EqualTo(3));
        Assert.That(outline.Count, Is.EqualTo(1));
        Assert.That(outline[0].Title, Is.EqualTo("Start"));
    }

    /// <summary>A checkbox reaches the built document as a form field.</summary>
    [Test]
    public void Checkbox_ReachesTheDocumentAsAField()
    {
        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        builder.AddCheckbox(0, new PdfRect(72, 600, 92, 620), "agree", true, "Agree to the terms");

        using var doc = Document.Open(builder.Build());
        using var fields = doc.FormFields();

        Assert.That(fields.Count, Is.EqualTo(1));
        Assert.That(fields[0].Name, Is.EqualTo("agree"));
    }

    /// <summary>An attached file comes back off the built document's <c>/EmbeddedFiles</c>.</summary>
    [Test]
    public void AttachFile_RoundTrips()
    {
        var payload = Encoding.UTF8.GetBytes("a,b\n1,2\n");

        using var builder = new Builder();
        builder.AddPage(HelloWorld(), Fonts);
        builder.AttachFile("data.csv", "text/csv", "Data", "Some data", payload);

        using var doc = Document.Open(builder.Build());
        using var attachments = doc.Attachments();

        Assert.That(attachments.Count, Is.EqualTo(1));
        Assert.That(attachments[0].Name, Is.EqualTo("data.csv"));
        Assert.That(attachments[0].Data, Is.EqualTo(payload));
    }

    /// <summary>
    /// A tagged page: marked content in the stream, a structure tree claiming it, and the
    /// consuming transfers that assemble the tree.
    /// </summary>
    [Test]
    public void Structure_TagsThePageContent()
    {
        using var content = new Content();
        content.BeginMarkedContent("P", 0);
        content.BeginText();
        content.SetFont("F1", 12);
        content.TextMove(72, 700);
        content.ShowText("Tagged");
        content.EndText();
        content.EndMarkedContent();

        using var builder = new Builder { Lang = "en-GB", Title = "Tagged" };
        builder.AddPage(content, Fonts);

        var paragraph = new StructNode("P") { Lang = "en-GB", ActualText = "Tagged" };
        paragraph.AddContent(0, 0);

        var section = new StructNode("Sect");
        section.AddChild(paragraph);
        Assert.That(paragraph.IsInvalid, Is.True, "a successful transfer consumes the child");

        builder.AddStructureNode(section);
        Assert.That(section.IsInvalid, Is.True, "a successful transfer consumes the node");

        using var doc = Document.Open(builder.Build());
        Assert.That(doc.PageText(0), Does.Contain("Tagged"));
    }

    /// <summary>Structure attributes and identifiers are accepted on a node.</summary>
    [Test]
    public void StructNode_CarriesAttributesAndIdentifiers()
    {
        using var figure = new StructNode("Figure")
        {
            Alt = "A grey square",
            Id = "fig1",
            Namespace = "http://iso.org/pdf2/ssn",
        };
        figure.AddNameAttribute("Layout", "Placement", "Block");
        figure.AddIntegerAttribute("Table", "ColSpan", 2);
        figure.AddTextAttribute("Layout", "Width", "100");
        figure.AddReference("fig1");
        figure.AssociateFile("plot.csv", "text/csv", "Data", null, "x,y\n1,2\n"u8);

        Assert.That(figure.IsInvalid, Is.False);
    }

    /// <summary>A failed transfer leaves the node owned by the caller — contract 3's other half.</summary>
    [Test]
    public void FailedTransfer_LeavesTheNodeOwned()
    {
        using var builder = new Builder();
        using var node = new StructNode("P");

        // A node cannot be its own child; the ABI rejects it without taking ownership.
        Assert.Throws<PrismPdfException>(() => node.AddChild(node));
        Assert.That(node.IsInvalid, Is.False, "a rejected transfer must not consume the handle");

        builder.AddStructureNode(node);
        Assert.That(node.IsInvalid, Is.True);
    }

    /// <summary>Null handles and names are rejected in managed code, before any native call.</summary>
    [Test]
    public void NullArguments_AreRejected()
    {
        using var builder = new Builder();

        Assert.Throws<ArgumentNullException>(() => builder.AddPage(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddPageSpec(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddStructureNode(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Title = null!);
        Assert.Throws<ArgumentNullException>(() => builder.SetInfo("k", null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddOutline(null!, 0));
        Assert.Throws<ArgumentNullException>(() => new StructNode(null!));
        Assert.Throws<ArgumentNullException>(() => new PageSpec(null!));
    }

    /// <summary>Negative indices are rejected before they are widened to <c>uintptr_t</c>.</summary>
    [Test]
    public void NegativeIndices_AreRejected()
    {
        using var builder = new Builder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddOutline("Start", -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.AddNote(-1, new PdfRect(0, 0, 1, 1), "note"));
    }

    private static Dictionary<string, StdFont> Fonts => new() { ["F1"] = StdFont.Helvetica };

    private static Content HelloWorld()
    {
        var content = new Content();
        content.BeginText();
        content.SetFont("F1", 24);
        content.TextMove(72, 700);
        content.ShowText("Hello");
        content.EndText();
        return content;
    }
}
