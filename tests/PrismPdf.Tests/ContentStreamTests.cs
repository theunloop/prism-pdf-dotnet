using System.Text;

namespace PrismPdf.Tests;

/// <summary>
/// The content-stream layer: one operator per method, assembled into the bytes a page carries.
/// </summary>
/// <remarks>
/// A content stream owns no document, so these assertions read the emitted operators directly
/// rather than reopening a PDF — that comes with the create journey, once the builder is bound.
/// </remarks>
[TestFixture]
public sealed class ContentStreamTests : NativeTestBase
{
    /// <summary>A fresh stream is empty, and lends back an empty array rather than null.</summary>
    [Test]
    public void NewContent_IsEmpty()
    {
        using var content = new Content();

        Assert.That(content.Bytes, Is.Empty);
    }

    /// <summary>Each operator appends; the bytes read back in the order they were written.</summary>
    [Test]
    public void Operators_AppendInOrder()
    {
        using var content = new Content();
        content.Save();
        content.SetLineWidth(2);
        content.MoveTo(0, 0);
        content.LineTo(100, 100);
        content.Stroke();
        content.Restore();

        var text = Text(content);

        Assert.That(text, Does.StartWith("q"));
        Assert.That(text, Does.Contain("2 w"));
        Assert.That(text, Does.Contain("0 0 m"));
        Assert.That(text, Does.Contain("100 100 l"));
        Assert.That(text, Does.Contain("S"));
        Assert.That(text.TrimEnd(), Does.EndWith("Q"));
    }

    /// <summary>Path construction and the three painting operators reach the stream.</summary>
    [Test]
    public void Paths_EmitTheirOperators()
    {
        using var content = new Content();
        content.Rect(10, 20, 30, 40);
        content.Fill();
        content.MoveTo(0, 0);
        content.CurveTo(1, 2, 3, 4, 5, 6);
        content.ClosePath();
        content.FillAndStroke();

        var text = Text(content);

        Assert.That(text, Does.Contain("10 20 30 40 re"));
        Assert.That(text, Does.Contain("1 2 3 4 5 6 c"));
        Assert.That(text, Does.Match(@"(^|\s)f(\s|$)"));
        Assert.That(text, Does.Contain("h"));
        Assert.That(text, Does.Contain("B"));
    }

    /// <summary>Every colour operator, in all four spaces the ABI exposes directly.</summary>
    [Test]
    public void Colors_EmitTheirOperators()
    {
        using var content = new Content();
        content.SetFillGray(0.5);
        content.SetStrokeGray(0.25);
        content.SetFillRgb(1, 0, 0);
        content.SetStrokeRgb(0, 1, 0);
        content.SetFillCmyk(0, 0, 0, 1);

        var text = Text(content);

        Assert.That(text, Does.Contain("0.5 g"));
        Assert.That(text, Does.Contain("0.25 G"));
        Assert.That(text, Does.Contain("1 0 0 rg"));
        Assert.That(text, Does.Contain("0 1 0 RG"));
        Assert.That(text, Does.Contain("0 0 0 1 k"));
    }

    /// <summary>A named colour space plus its components (<c>cs</c> / <c>sc</c>).</summary>
    [Test]
    public void FillColor_InANamedSpace()
    {
        using var content = new Content();
        content.SetFillColorSpace("Spot");
        content.SetFillColor([0.4]);

        var text = Text(content);

        Assert.That(text, Does.Contain("/Spot cs"));
        Assert.That(text, Does.Contain("0.4 sc"));
    }

    /// <summary>A text object: font, positioning, and a string shown through the string overload.</summary>
    [Test]
    public void Text_WritesAShowOperator()
    {
        using var content = new Content();
        content.BeginText();
        content.SetFont("F1", 12);
        content.SetLeading(14);
        content.SetCharSpacing(0.1);
        content.SetWordSpacing(0.2);
        content.TextMove(72, 720);
        content.ShowText("Hello");
        content.NextLine();
        content.SetTextMatrix(1, 0, 0, 1, 72, 700);
        content.EndText();

        var text = Text(content);

        Assert.That(text, Does.Contain("BT"));
        Assert.That(text, Does.Contain("/F1 12 Tf"));
        Assert.That(text, Does.Contain("14 TL"));
        Assert.That(text, Does.Contain("0.1 Tc"));
        Assert.That(text, Does.Contain("0.2 Tw"));
        Assert.That(text, Does.Contain("72 720 Td"));
        Assert.That(text, Does.Contain("(Hello) Tj"));
        Assert.That(text, Does.Contain("T*"));
        Assert.That(text, Does.Contain("1 0 0 1 72 700 Tm"));
        Assert.That(text, Does.Contain("ET"));
    }

    /// <summary>The byte overload writes character codes through unchanged.</summary>
    [Test]
    public void ShowText_WithBytes_WritesRawCodes()
    {
        using var content = new Content();
        content.BeginText();
        content.SetFont("F1", 12);
        content.ShowText("Raw"u8);
        content.EndText();

        Assert.That(Text(content), Does.Contain("(Raw) Tj"));
    }

    /// <summary>Empty payloads are legal: the ABI takes a null pointer with length 0.</summary>
    [Test]
    public void EmptyPayloads_AreAccepted()
    {
        using var content = new Content();
        content.BeginText();
        content.SetFont("F1", 12);
        content.ShowText(ReadOnlySpan<byte>.Empty);
        content.ShowGlyphs(ReadOnlySpan<ushort>.Empty);
        content.EndText();

        Assert.That(content.Bytes, Is.Not.Empty);
    }

    /// <summary>Marked content, artifacts, and their common close operator.</summary>
    [Test]
    public void MarkedContent_TagsAndArtifacts()
    {
        using var content = new Content();
        content.BeginMarkedContent("P", 0);
        content.EndMarkedContent();
        content.BeginArtifact();
        content.EndMarkedContent();

        var text = Text(content);

        Assert.That(text, Does.Contain("/P <</MCID 0>> BDC"));
        Assert.That(text, Does.Contain("/Artifact BMC"));
        Assert.That(text, Does.Contain("EMC"));
    }

    /// <summary>An XObject invocation names a page resource.</summary>
    [Test]
    public void DoXObject_NamesTheResource()
    {
        using var content = new Content();
        content.DoXObject("Im1");

        Assert.That(Text(content), Does.Contain("/Im1 Do"));
    }

    /// <summary>An inline image carries its dictionary and then its samples.</summary>
    [Test]
    public void InlineImage_EmitsDictionaryAndSamples()
    {
        using var content = new Content();
        content.InlineImage(2, 2, "G", 8, [0x00, 0x40, 0x80, 0xFF]);

        var text = Text(content);

        Assert.That(text, Does.Contain("BI"));
        Assert.That(text, Does.Contain("/W 2"));
        Assert.That(text, Does.Contain("/H 2"));
        Assert.That(text, Does.Contain("ID"));
        Assert.That(text, Does.Contain("EI"));
    }

    /// <summary>The transform operator concatenates onto the CTM.</summary>
    [Test]
    public void Transform_EmitsCm()
    {
        using var content = new Content();
        content.Transform(2, 0, 0, 2, 10, 20);

        Assert.That(Text(content), Does.Contain("2 0 0 2 10 20 cm"));
    }

    /// <summary>Reading the bytes twice returns equal copies — the view is copied, not handed out.</summary>
    [Test]
    public void Bytes_AreCopiedOnEachRead()
    {
        using var content = new Content();
        content.MoveTo(1, 2);

        var first = content.Bytes;
        var second = content.Bytes;

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Is.Not.SameAs(second));
    }

    /// <summary>A disposed stream raises from managed code rather than calling into the library.</summary>
    [Test]
    public void DisposedContent_RaisesObjectDisposed()
    {
        var content = new Content();
        content.Dispose();

        Assert.That(content.IsInvalid, Is.True);
        Assert.Throws<ObjectDisposedException>(() => content.MoveTo(0, 0));
        Assert.Throws<ObjectDisposedException>(() => _ = content.Bytes);
    }

    /// <summary>Disposing twice is a no-op, as the disposal idiom requires.</summary>
    [Test]
    public void Dispose_IsIdempotent()
    {
        var content = new Content();
        content.Dispose();

        Assert.DoesNotThrow(content.Dispose);
    }

    /// <summary>Null names are rejected in managed code, with the parameter named.</summary>
    [Test]
    public void NullArguments_AreRejected()
    {
        using var content = new Content();

        Assert.Throws<ArgumentNullException>(() => content.SetFont(null!, 12));
        Assert.Throws<ArgumentNullException>(() => content.ShowText((string)null!));
        Assert.Throws<ArgumentNullException>(() => content.DoXObject(null!));
        Assert.Throws<ArgumentNullException>(() => content.SetFillColorSpace(null!));
        Assert.Throws<ArgumentNullException>(() => content.BeginMarkedContent(null!, 0));
        Assert.Throws<ArgumentNullException>(() => content.BeginAfMarkedContent(null!));
        Assert.Throws<ArgumentNullException>(() => content.InlineImage(1, 1, null!, 8, []));
    }

    private static string Text(Content content) => Encoding.ASCII.GetString(content.Bytes);
}
