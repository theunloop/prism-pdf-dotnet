namespace PrismPdf.Tests;

/// <summary>
/// Declarative composition: the arena model, the container generations, and the anchor test —
/// a port of the core's <c>compose_invoice.c</c>, the tagged multipage invoice every binding
/// builds.
/// </summary>
[TestFixture]
public sealed class CompositionTests : NativeTestBase
{
    /// <summary>The smallest composition: one page, one text node.</summary>
    [Test]
    public void Composition_BuildsAPageOfText()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        page.SetText("Composed.", 12, 15);

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageCount, Is.EqualTo(1));
        Assert.That(doc.PageText(0), Does.Contain("Composed."));
    }

    /// <summary>Columns stack, rows divide, and both nest.</summary>
    [Test]
    public void Containers_NestColumnsAndRows()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        using var column = page.SetColumn(10);

        using var first = column.AddItem();
        first.SetText("Top of the column.", 12, 15);

        using var second = column.AddItem();
        using var row = second.SetRow();
        using var left = row.AddFixed(120);
        left.SetText("Fixed left.", 10, 13);
        using var middle = row.AddRelative(1);
        middle.SetText("Relative middle.", 10, 13);
        using var right = row.AddAuto();
        right.SetText("Auto right.", 10, 13);

        using var doc = Document.Open(composition.Build());
        var text = doc.PageText(0);

        Assert.That(text, Does.Contain("Top of the column."));
        Assert.That(text, Does.Contain("Fixed left."));
        Assert.That(text, Does.Contain("Relative middle."));
        Assert.That(text, Does.Contain("Auto right."));
    }

    /// <summary>Filling a slot spends its handle; the engine reports <c>InvalidUse</c>.</summary>
    [Test]
    public void FillingASlot_SpendsTheContainerGeneration()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));

        using var column = page.SetColumn(10);

        var ex = Assert.Throws<PrismPdfException>(() => page.SetColumn(10));
        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.InvalidUse));
        Assert.That(page.IsInvalid, Is.False,
            "the handle is spent in the arena, not disposed — it is still ours to release");
    }

    /// <summary>Appending to a column does <em>not</em> spend the handle.</summary>
    [Test]
    public void AppendingToAColumn_LeavesTheHandleUsable()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        using var column = page.SetColumn(6);

        for (var i = 0; i < 5; i++)
        {
            using var item = column.AddItem();
            item.SetText($"Item {i}.", 11, 14);
        }

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageText(0), Does.Contain("Item 4."));
    }

    /// <summary>
    /// Containers outlive their scope in the arena: disposing one releases the handle, not the
    /// node, so a tree written with nested <c>using</c> still builds completely.
    /// </summary>
    [Test]
    public void DisposingAContainer_LeavesItsNodeInTheComposition()
    {
        using var composition = new Composition();

        using (var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48)))
        {
            using var column = page.SetColumn(8);
            using var item = column.AddItem();
            item.SetText("Written inside a scope.", 12, 15);
        }

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageText(0), Does.Contain("Written inside a scope."));
    }

    /// <summary>A container survives its composition being released, and is still safe to dispose.</summary>
    [Test]
    public void ReleasingTheComposition_LeavesContainersSafeToDispose()
    {
        var composition = new Composition();
        var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));

        composition.Dispose();

        Assert.DoesNotThrow(page.Dispose);
    }

    /// <summary>Tables paginate, and their header row repeats on every fragment.</summary>
    [Test]
    public void Table_RepeatsItsHeaderAcrossFragments()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(36));
        using var table = page.SetTable();
        table.AddRelativeColumn(2);
        table.AddFixedColumn(80);
        table.AddAutoColumn();

        using (var header = table.SetHeader())
        {
            foreach (var caption in new[] { "Description", "Amount", "Tax" })
            {
                using var cell = header.AddCell();
                cell.SetText(caption, 10, 13);
            }
        }

        for (var i = 0; i < 60; i++)
        {
            using var row = table.AddRow();
            using (var description = row.AddCell())
            {
                description.SetText($"Line item {i}", 10, 13);
            }

            using (var amount = row.AddCell())
            {
                amount.SetText($"{i * 3}.00", 10, 13);
            }

            using var tax = row.AddCell();
            tax.SetText("20%", 10, 13);
        }

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageCount, Is.GreaterThan(1), "60 rows do not fit on one A4 page");
        Assert.That(doc.PageText(0), Does.Contain("Description"));
        Assert.That(doc.PageText(doc.PageCount - 1), Does.Contain("Description"),
            "the header row repeats on each fragment");
    }

    /// <summary>Padding, sizing, alignment, borders and backgrounds all wrap a child slot.</summary>
    [Test]
    public void BoxDecoration_WrapsItsChild()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        using var column = page.SetColumn(8);

        using var boxed = column.AddItem();
        using var background = boxed.SetBackground(PdfColor.FromBytes(0xEE, 0xEE, 0xEE));
        using var border = background.SetBorder(1, PdfColor.Black);
        using var padded = border.SetPadding(8);
        using var sized = padded.SetWidth(300);
        using var tall = sized.SetHeight(60);
        using var aligned = tall.SetAlignment(
            CompositionHorizontalAlign.Center, CompositionVerticalAlign.Center);
        aligned.SetText("Boxed.", 12, 15);

        using var extended = column.AddItem();
        using var stretch = extended.SetExtend();
        stretch.SetText("Extended.", 10, 13);

        using var doc = Document.Open(composition.Build());
        var text = doc.PageText(0);

        Assert.That(text, Does.Contain("Boxed."));
        Assert.That(text, Does.Contain("Extended."));
    }

    /// <summary>An explicit page break fills a slot and starts the next page.</summary>
    [Test]
    public void PageBreak_StartsANewPage()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        using var column = page.SetColumn(8);

        using (var before = column.AddItem())
        {
            before.SetText("Before.", 12, 15);
        }

        using (var brk = column.AddItem())
        {
            brk.SetPageBreak();
        }

        using var after = column.AddItem();
        after.SetText("After.", 12, 15);

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageCount, Is.EqualTo(2));
        Assert.That(doc.PageText(0), Does.Contain("Before."));
        Assert.That(doc.PageText(1), Does.Contain("After."));
    }

    /// <summary>Headers and footers repeat, and their placeholders are substituted.</summary>
    [Test]
    public void HeaderAndFooter_RepeatWithPlaceholders()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));

        using (var header = composition.SetPageHeader(0))
        {
            header.SetText("Acme Ltd", 9, 11);
        }

        using (var footer = composition.SetPageFooter(0))
        {
            footer.SetText("Page {page} of {pages}", 9, 11);
        }

        using var column = page.SetColumn(8);
        using (var first = column.AddItem())
        {
            first.SetText("First page.", 12, 15);
        }

        using (var brk = column.AddItem())
        {
            brk.SetPageBreak();
        }

        using var second = column.AddItem();
        second.SetText("Second page.", 12, 15);

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageCount, Is.EqualTo(2));
        Assert.That(doc.PageText(0), Does.Contain("Acme Ltd"));
        Assert.That(doc.PageText(1), Does.Contain("Acme Ltd"));
        Assert.That(doc.PageText(0), Does.Contain("Page 1 of 2"));
        Assert.That(doc.PageText(1), Does.Contain("Page 2 of 2"));
    }

    /// <summary>An image fills a slot, and the built page carries it.</summary>
    [Test]
    public void Image_FillsASlot()
    {
        using var image = ImageSource.FromGray(2, 2, [0x00, 0x40, 0x80, 0xFF]);
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        using var column = page.SetColumn(8);

        using (var fit = column.AddItem())
        {
            fit.SetImage(image, CompositionImageSizing.Fit, 80, 80);
        }

        using (var fill = column.AddItem())
        {
            fill.SetImage(image, CompositionImageSizing.Fill, 80, 40);
        }

        using var exact = column.AddItem();
        exact.SetImage(image, CompositionImageSizing.Exact, 60, 60);

        using var doc = Document.Open(composition.Build());
        using var images = doc.PageImages(0);

        Assert.That(images.Count, Is.EqualTo(3));
    }

    /// <summary>Tagging wraps content in structure roles: headings, figures, links, list parts.</summary>
    [Test]
    public void Tagged_WrapsContentInStructureRoles()
    {
        using var image = ImageSource.FromGray(2, 2, [0x00, 0x40, 0x80, 0xFF]);
        using var composition = new Composition { TaggedLanguage = "en-GB" };
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        using var column = page.SetColumn(10);

        using (var headingSlot = column.AddItem())
        {
            using var heading = headingSlot.SetHeading(1);
            heading.SetText("A heading", 18, 22);
        }

        using (var paragraphSlot = column.AddItem())
        {
            using var paragraph = paragraphSlot.SetSemantic(CompositionSemantic.Paragraph);
            paragraph.SetText("A tagged paragraph.", 11, 14);
        }

        using (var listSlot = column.AddItem())
        {
            using var list = listSlot.SetSemantic(CompositionSemantic.List);
            using var listColumn = list.SetColumn(4);
            using var itemSlot = listColumn.AddItem();
            using var item = itemSlot.SetSemantic(CompositionSemantic.ListItem);
            item.SetText("An item.", 11, 14);
        }

        using (var linkSlot = column.AddItem())
        {
            using var link = linkSlot.SetLink("https://example.invalid", "The example site");
            link.SetText("example.invalid", 11, 14);
        }

        using var figureSlot = column.AddItem();
        using var figure = figureSlot.SetFigure("A grey gradient");
        figure.SetImage(image, CompositionImageSizing.Fit, 60, 60);

        using var doc = Document.Open(composition.Build());
        var text = doc.PageText(0);

        Assert.That(text, Does.Contain("A heading"));
        Assert.That(text, Does.Contain("A tagged paragraph."));
        Assert.That(text, Does.Contain("An item."));

        using var annotations = doc.PageAnnotations(0);
        Assert.That(annotations.Count, Is.EqualTo(1), "the link role emits a link annotation");
    }

    /// <summary>Building twice is refused: the composition is immutable once built.</summary>
    [Test]
    public void Build_IsOneWay()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
        page.SetText("Once.", 12, 15);

        var bytes = composition.Build();
        Assert.That(bytes, Is.Not.Empty);

        var ex = Assert.Throws<PrismPdfException>(
            () => composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48)));
        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.InvalidUse),
            "a built composition refuses every further edit, not just the second build");
        Assert.Throws<PrismPdfException>(() => composition.Build());
    }

    /// <summary>
    /// The anchor acceptance test: the tagged multipage invoice from the core's
    /// <c>compose_invoice.c</c>, asserted on by reopening it.
    /// </summary>
    [Test]
    public void ComposeInvoice_IsTheAnchorAcceptanceTest()
    {
        using var composition = new Composition { TaggedLanguage = "en-GB" };
        using var page = composition.AddPage(PdfSize.A4, new PdfMargins(56, 48, 56, 48));

        using (var header = composition.SetPageHeader(0))
        {
            header.SetText("Acme Ltd — Invoice 2026-0042", 9, 11);
        }

        using (var footer = composition.SetPageFooter(0))
        {
            footer.SetText("Page {page} of {pages}", 9, 11);
        }

        using var body = page.SetColumn(14);

        using (var titleSlot = body.AddItem())
        {
            using var title = titleSlot.SetHeading(1);
            title.SetText("Invoice", 22, 26);
        }

        using (var partiesSlot = body.AddItem())
        {
            using var parties = partiesSlot.SetRow();
            using var from = parties.AddRelative(1);
            from.SetText("From: Acme Ltd, 1 Anvil Way", 10, 13);
            using var to = parties.AddRelative(1);
            to.SetText("To: Wile E. Coyote, Desert Rd", 10, 13);
        }

        using (var tableSlot = body.AddItem())
        {
            using var table = tableSlot.SetTable();
            table.AddRelativeColumn(3);
            table.AddFixedColumn(60);
            table.AddFixedColumn(80);

            using (var header = table.SetHeader())
            {
                foreach (var caption in new[] { "Description", "Qty", "Amount" })
                {
                    using var cell = header.AddCell();
                    cell.SetText(caption, 10, 13);
                }
            }

            for (var i = 1; i <= 45; i++)
            {
                using var row = table.AddRow();
                using (var description = row.AddCell())
                {
                    description.SetText($"Rocket-powered device, model {i}", 10, 13);
                }

                using (var quantity = row.AddCell())
                {
                    quantity.SetText(i.ToString(), 10, 13);
                }

                using var amount = row.AddCell();
                amount.SetText($"£{i * 19}.00", 10, 13);
            }
        }

        using (var totalSlot = body.AddItem())
        {
            using var boxed = totalSlot.SetBorder(1, PdfColor.Black);
            using var padded = boxed.SetPadding(6);
            padded.SetText("Total due: £19,665.00", 12, 15);
        }

        using var termsSlot = body.AddItem();
        using var terms = termsSlot.SetSemantic(CompositionSemantic.Paragraph);
        terms.SetText("Payment due within 30 days.", 9, 12);

        using var doc = Document.Open(composition.Build());

        Assert.That(doc.PageCount, Is.GreaterThan(1), "45 line items paginate");
        Assert.That(doc.PageText(0), Does.Contain("Invoice"));
        Assert.That(doc.PageText(0), Does.Contain("Description"));
        Assert.That(doc.PageText(0), Does.Contain("Rocket-powered device, model 1"));
        Assert.That(doc.Text, Does.Contain("Rocket-powered device, model 45"));
        Assert.That(doc.Text, Does.Contain("Total due"));
        Assert.That(doc.PageText(doc.PageCount - 1), Does.Contain("Payment due within 30 days."),
            "the closing paragraph is pushed past the table's last fragment");

        for (var i = 0; i < doc.PageCount; i++)
        {
            Assert.That(doc.PageText(i), Does.Contain("Acme Ltd"), $"header missing on page {i}");
            Assert.That(doc.PageText(i), Does.Contain($"Page {i + 1} of {doc.PageCount}"));
        }
    }

    /// <summary>Null arguments are rejected in managed code, before any native call.</summary>
    [Test]
    public void NullArguments_AreRejected()
    {
        using var composition = new Composition();
        using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));

        Assert.Throws<ArgumentNullException>(() => composition.TaggedLanguage = null!);
        Assert.Throws<ArgumentNullException>(() => page.SetText(null!, 10, 12));
        Assert.Throws<ArgumentNullException>(() => page.SetImage(null!, CompositionImageSizing.Fit, 1, 1));
        Assert.Throws<ArgumentNullException>(() => page.SetFigure(null!));
        Assert.Throws<ArgumentNullException>(() => page.SetLink(null!, "description"));
    }
}
