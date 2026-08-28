using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A page content stream under construction: the operators of §8 and §9, appended in order.
/// </summary>
/// <remarks>
/// <para>
/// This is the authoring counterpart to the read path. Nothing here touches a document — a content
/// stream is just a growing buffer of operator bytes, which <see cref="Bytes"/> hands back and a
/// builder writes into a page. It owns no document, borrows no document, and outlives none.
/// </para>
/// <para>
/// Every method maps to exactly one operator, named as the ABI names it. The engine validates as
/// it goes (an unbalanced <c>BT</c>/<c>ET</c>, a colour component out of range), so a malformed
/// stream raises <see cref="PrismPdfException"/> at the offending call rather than at save time.
/// </para>
/// <para>
/// Not thread-safe, per semantic contract 6: confine an instance to one thread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var content = new Content();
/// content.BeginText();
/// content.SetFont("F1", 24);
/// content.TextMove(72, 700);
/// content.ShowText("Hello");
/// content.EndText();
/// </code>
/// </example>
public sealed unsafe class Content : PrismPdfHandle
{
    /// <summary>Create an empty content stream.</summary>
    public Content()
        : base(NativeMethods.prismpdf_content_new())
    {
    }

    /// <summary>
    /// The operator bytes assembled so far.
    /// </summary>
    /// <remarks>
    /// The ABI lends this view rather than transferring it — it dies with the handle — so this
    /// property copies it. Reading is therefore O(n) in the stream's length; keep it out of a
    /// loop that is still appending. An empty stream reads back as an empty array.
    /// </remarks>
    public byte[] Bytes
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_content_bytes(Handle, &data, &len),
                "prismpdf_content_bytes");
            return Native.CopyBorrowedBytes(data, len);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Graphics state (8.4)
    // -----------------------------------------------------------------------------------------

    /// <summary>Push the graphics state (<c>q</c>, §8.4.4).</summary>
    public void Save()
        => Native.Check(NativeMethods.prismpdf_content_save(Handle), "prismpdf_content_save");

    /// <summary>Pop the graphics state (<c>Q</c>, §8.4.4).</summary>
    public void Restore()
        => Native.Check(NativeMethods.prismpdf_content_restore(Handle), "prismpdf_content_restore");

    /// <summary>Concatenate a matrix onto the current transformation (<c>cm</c>, §8.3.3).</summary>
    /// <param name="a">Row 1, column 1.</param>
    /// <param name="b">Row 1, column 2.</param>
    /// <param name="c">Row 2, column 1.</param>
    /// <param name="d">Row 2, column 2.</param>
    /// <param name="e">Horizontal translation.</param>
    /// <param name="f">Vertical translation.</param>
    public void Transform(double a, double b, double c, double d, double e, double f)
        => Native.Check(NativeMethods.prismpdf_content_transform(Handle, a, b, c, d, e, f),
            "prismpdf_content_transform");

    /// <summary>Set the stroke width (<c>w</c>, §8.4.3.2).</summary>
    /// <param name="width">Line width in user-space units.</param>
    public void SetLineWidth(double width)
        => Native.Check(NativeMethods.prismpdf_content_set_line_width(Handle, width),
            "prismpdf_content_set_line_width");

    // -----------------------------------------------------------------------------------------
    // Colour (8.6)
    // -----------------------------------------------------------------------------------------

    /// <summary>Set a <c>DeviceGray</c> fill colour (<c>g</c>, §8.6.4.2).</summary>
    /// <param name="gray">Grey level, 0 (black) to 1 (white).</param>
    public void SetFillGray(double gray)
        => Native.Check(NativeMethods.prismpdf_content_set_fill_gray(Handle, gray),
            "prismpdf_content_set_fill_gray");

    /// <summary>Set a <c>DeviceGray</c> stroke colour (<c>G</c>, §8.6.4.2).</summary>
    /// <param name="gray">Grey level, 0 (black) to 1 (white).</param>
    public void SetStrokeGray(double gray)
        => Native.Check(NativeMethods.prismpdf_content_set_stroke_gray(Handle, gray),
            "prismpdf_content_set_stroke_gray");

    /// <summary>Set a <c>DeviceRGB</c> fill colour (<c>rg</c>, §8.6.4.3).</summary>
    /// <param name="r">Red, 0 to 1.</param>
    /// <param name="g">Green, 0 to 1.</param>
    /// <param name="b">Blue, 0 to 1.</param>
    public void SetFillRgb(double r, double g, double b)
        => Native.Check(NativeMethods.prismpdf_content_set_fill_rgb(Handle, r, g, b),
            "prismpdf_content_set_fill_rgb");

    /// <summary>Set a <c>DeviceRGB</c> stroke colour (<c>RG</c>, §8.6.4.3).</summary>
    /// <param name="r">Red, 0 to 1.</param>
    /// <param name="g">Green, 0 to 1.</param>
    /// <param name="b">Blue, 0 to 1.</param>
    public void SetStrokeRgb(double r, double g, double b)
        => Native.Check(NativeMethods.prismpdf_content_set_stroke_rgb(Handle, r, g, b),
            "prismpdf_content_set_stroke_rgb");

    /// <summary>Set a <c>DeviceCMYK</c> fill colour (<c>k</c>, §8.6.4.4).</summary>
    /// <param name="c">Cyan, 0 to 1.</param>
    /// <param name="m">Magenta, 0 to 1.</param>
    /// <param name="y">Yellow, 0 to 1.</param>
    /// <param name="k">Black, 0 to 1.</param>
    public void SetFillCmyk(double c, double m, double y, double k)
        => Native.Check(NativeMethods.prismpdf_content_set_fill_cmyk(Handle, c, m, y, k),
            "prismpdf_content_set_fill_cmyk");

    /// <summary>
    /// Select a fill colour space by resource name (<c>cs</c>, §8.6.8).
    /// </summary>
    /// <param name="name">
    /// A key in the page's <c>/Resources /ColorSpace</c> — for instance one added by
    /// <c>builder.AddSeparation</c>.
    /// </param>
    public void SetFillColorSpace(string name)
        => CallWithText(NativeMethods.prismpdf_content_set_fill_color_space, name, nameof(name),
            "prismpdf_content_set_fill_color_space");

    /// <summary>
    /// Set fill-colour components in the current colour space (<c>sc</c>, §8.6.8).
    /// </summary>
    /// <param name="components">
    /// One value for a Separation or Gray space, three for RGB, four for CMYK.
    /// </param>
    public void SetFillColor(ReadOnlySpan<double> components)
    {
        fixed (double* values = components)
        {
            Native.Check(
                NativeMethods.prismpdf_content_set_fill_color(Handle, values, (nuint)components.Length),
                "prismpdf_content_set_fill_color");
        }
    }

    // -----------------------------------------------------------------------------------------
    // Path construction and painting (8.5)
    // -----------------------------------------------------------------------------------------

    /// <summary>Begin a new subpath at <c>(x, y)</c> (<c>m</c>, §8.5.2.1).</summary>
    /// <param name="x">Horizontal coordinate.</param>
    /// <param name="y">Vertical coordinate.</param>
    public void MoveTo(double x, double y)
        => Native.Check(NativeMethods.prismpdf_content_move_to(Handle, x, y),
            "prismpdf_content_move_to");

    /// <summary>Append a straight segment to <c>(x, y)</c> (<c>l</c>, §8.5.2.1).</summary>
    /// <param name="x">Horizontal coordinate.</param>
    /// <param name="y">Vertical coordinate.</param>
    public void LineTo(double x, double y)
        => Native.Check(NativeMethods.prismpdf_content_line_to(Handle, x, y),
            "prismpdf_content_line_to");

    /// <summary>Append a cubic Bézier with both control points (<c>c</c>, §8.5.2.2).</summary>
    /// <param name="x1">First control point, horizontal.</param>
    /// <param name="y1">First control point, vertical.</param>
    /// <param name="x2">Second control point, horizontal.</param>
    /// <param name="y2">Second control point, vertical.</param>
    /// <param name="x3">End point, horizontal.</param>
    /// <param name="y3">End point, vertical.</param>
    public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        => Native.Check(NativeMethods.prismpdf_content_curve_to(Handle, x1, y1, x2, y2, x3, y3),
            "prismpdf_content_curve_to");

    /// <summary>Append a complete rectangular subpath (<c>re</c>, §8.5.2.1).</summary>
    /// <param name="x">Lower-left corner, horizontal.</param>
    /// <param name="y">Lower-left corner, vertical.</param>
    /// <param name="w">Width.</param>
    /// <param name="h">Height.</param>
    public void Rect(double x, double y, double w, double h)
        => Native.Check(NativeMethods.prismpdf_content_rect(Handle, x, y, w, h),
            "prismpdf_content_rect");

    /// <summary>Close the current subpath (<c>h</c>, §8.5.2.1).</summary>
    public void ClosePath()
        => Native.Check(NativeMethods.prismpdf_content_close_path(Handle),
            "prismpdf_content_close_path");

    /// <summary>Stroke the current path (<c>S</c>, §8.5.3.1).</summary>
    public void Stroke()
        => Native.Check(NativeMethods.prismpdf_content_stroke(Handle), "prismpdf_content_stroke");

    /// <summary>Fill the current path, non-zero winding (<c>f</c>, §8.5.3.3).</summary>
    public void Fill()
        => Native.Check(NativeMethods.prismpdf_content_fill(Handle), "prismpdf_content_fill");

    /// <summary>Fill and then stroke the current path (<c>B</c>, §8.5.3.1).</summary>
    public void FillAndStroke()
        => Native.Check(NativeMethods.prismpdf_content_fill_and_stroke(Handle),
            "prismpdf_content_fill_and_stroke");

    // -----------------------------------------------------------------------------------------
    // Text (9.3, 9.4)
    // -----------------------------------------------------------------------------------------

    /// <summary>Begin a text object (<c>BT</c>, §9.4.1).</summary>
    public void BeginText()
        => Native.Check(NativeMethods.prismpdf_content_begin_text(Handle),
            "prismpdf_content_begin_text");

    /// <summary>End a text object (<c>ET</c>, §9.4.1).</summary>
    public void EndText()
        => Native.Check(NativeMethods.prismpdf_content_end_text(Handle),
            "prismpdf_content_end_text");

    /// <summary>Set character spacing (<c>Tc</c>, §9.3.2).</summary>
    /// <param name="spacing">Extra space per character, in unscaled text units.</param>
    public void SetCharSpacing(double spacing)
        => Native.Check(NativeMethods.prismpdf_content_set_char_spacing(Handle, spacing),
            "prismpdf_content_set_char_spacing");

    /// <summary>Set word spacing (<c>Tw</c>, §9.3.3).</summary>
    /// <param name="spacing">Extra space per single-byte space character.</param>
    public void SetWordSpacing(double spacing)
        => Native.Check(NativeMethods.prismpdf_content_set_word_spacing(Handle, spacing),
            "prismpdf_content_set_word_spacing");

    /// <summary>Set the leading used by <see cref="NextLine"/> (<c>TL</c>, §9.3.5).</summary>
    /// <param name="leading">Vertical distance between baselines.</param>
    public void SetLeading(double leading)
        => Native.Check(NativeMethods.prismpdf_content_set_leading(Handle, leading),
            "prismpdf_content_set_leading");

    /// <summary>
    /// Select a font and size (<c>Tf</c>, §9.3.1).
    /// </summary>
    /// <param name="name">
    /// A key in the page's <c>/Resources /Font</c> — one of the names given to the builder when the
    /// page was added.
    /// </param>
    /// <param name="size">Font size in user-space units.</param>
    public void SetFont(string name, double size)
    {
        Throw.IfNull(name);

        var text = Native.AllocUtf8(name);
        try
        {
            Native.Check(NativeMethods.prismpdf_content_set_font(Handle, text, size),
                "prismpdf_content_set_font");
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    /// <summary>Move to the next line offset by <c>(tx, ty)</c> (<c>Td</c>, §9.4.2).</summary>
    /// <param name="tx">Horizontal offset from the start of the current line.</param>
    /// <param name="ty">Vertical offset from the start of the current line.</param>
    public void TextMove(double tx, double ty)
        => Native.Check(NativeMethods.prismpdf_content_text_move(Handle, tx, ty),
            "prismpdf_content_text_move");

    /// <summary>Replace the text and line matrices (<c>Tm</c>, §9.4.2).</summary>
    /// <param name="a">Row 1, column 1.</param>
    /// <param name="b">Row 1, column 2.</param>
    /// <param name="c">Row 2, column 1.</param>
    /// <param name="d">Row 2, column 2.</param>
    /// <param name="e">Horizontal translation.</param>
    /// <param name="f">Vertical translation.</param>
    public void SetTextMatrix(double a, double b, double c, double d, double e, double f)
        => Native.Check(NativeMethods.prismpdf_content_set_text_matrix(Handle, a, b, c, d, e, f),
            "prismpdf_content_set_text_matrix");

    /// <summary>Move to the start of the next line, using the leading (<c>T*</c>, §9.4.2).</summary>
    public void NextLine()
        => Native.Check(NativeMethods.prismpdf_content_next_line(Handle),
            "prismpdf_content_next_line");

    /// <summary>
    /// Show UTF-8 text (<c>Tj</c>, §9.4.3), encoded for the current Standard-14 font.
    /// </summary>
    /// <remarks>
    /// The overload taking bytes writes raw character codes instead, leaving the encoding to the
    /// caller — which is what a font embedded by the builder needs.
    /// </remarks>
    /// <param name="text">The text to show.</param>
    public void ShowText(string text)
        => CallWithText(NativeMethods.prismpdf_content_show_str, text, nameof(text),
            "prismpdf_content_show_str");

    /// <summary>
    /// Show a string of raw character codes (<c>Tj</c>, §9.4.3) — the bytes are written as given,
    /// so the caller controls the encoding.
    /// </summary>
    /// <param name="bytes">The character codes to show.</param>
    public void ShowText(ReadOnlySpan<byte> bytes)
    {
        fixed (byte* data = bytes)
        {
            Native.Check(NativeMethods.prismpdf_content_show_text(Handle, data, (nuint)bytes.Length),
                "prismpdf_content_show_text");
        }
    }

    /// <summary>
    /// Show pre-shaped glyph indices (<c>Tj</c> with two-byte codes, §9.4.3), for a composite font
    /// embedded as a CID font.
    /// </summary>
    /// <param name="glyphIds">The glyph indices, in the embedded font's own numbering.</param>
    public void ShowGlyphs(ReadOnlySpan<ushort> glyphIds)
    {
        fixed (ushort* gids = glyphIds)
        {
            Native.Check(
                NativeMethods.prismpdf_content_show_glyphs(Handle, gids, (nuint)glyphIds.Length),
                "prismpdf_content_show_glyphs");
        }
    }

    // -----------------------------------------------------------------------------------------
    // XObjects and inline images (8.8, 8.9)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Draw a named XObject (<c>Do</c>, §8.8) — an image or form from the page's
    /// <c>/Resources /XObject</c>.
    /// </summary>
    /// <param name="name">The resource name.</param>
    public void DoXObject(string name)
        => CallWithText(NativeMethods.prismpdf_content_do_xobject, name, nameof(name),
            "prismpdf_content_do_xobject");

    /// <summary>
    /// Emit an inline image (<c>BI … ID … EI</c>, §8.9.7).
    /// </summary>
    /// <param name="width">Width in samples.</param>
    /// <param name="height">Height in samples.</param>
    /// <param name="colorSpace">
    /// An abbreviated colour-space name: <c>G</c>, <c>RGB</c> or <c>CMYK</c>.
    /// </param>
    /// <param name="bitsPerComponent">Bits per component.</param>
    /// <param name="data">The raw samples.</param>
    public void InlineImage(
        int width,
        int height,
        string colorSpace,
        int bitsPerComponent,
        ReadOnlySpan<byte> data)
    {
        Throw.IfNull(colorSpace);
        Throw.IfNegative(width);
        Throw.IfNegative(height);
        Throw.IfNegative(bitsPerComponent);

        var cs = Native.AllocUtf8(colorSpace);
        try
        {
            fixed (byte* samples = data)
            {
                Native.Check(
                    NativeMethods.prismpdf_content_inline_image(
                        Handle, (uint)width, (uint)height, cs, (uint)bitsPerComponent,
                        samples, (nuint)data.Length),
                    "prismpdf_content_inline_image");
            }
        }
        finally
        {
            Native.FreeUtf8(cs);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Marked content (14.6, 14.8)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Open a marked-content sequence tying this content to structure element
    /// <paramref name="mcid"/> (<c>BDC</c>, §14.6) — how tagged content is associated with the
    /// structure tree.
    /// </summary>
    /// <param name="tag">The structure tag, e.g. <c>P</c> or <c>Figure</c>.</param>
    /// <param name="mcid">The marked-content identifier the structure node refers to.</param>
    public void BeginMarkedContent(string tag, int mcid)
    {
        Throw.IfNull(tag);
        Throw.IfNegative(mcid);

        var text = Native.AllocUtf8(tag);
        try
        {
            Native.Check(
                NativeMethods.prismpdf_content_begin_marked_content(Handle, text, (uint)mcid),
                "prismpdf_content_begin_marked_content");
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    /// <summary>
    /// Open a marked-content sequence associating this content with an embedded file (<c>BDC</c>
    /// with an <c>/AF</c> property, §14.13.9 — PDF 2.0).
    /// </summary>
    /// <param name="property">
    /// The name of a property entry added to the page's resources by the builder.
    /// </param>
    public void BeginAfMarkedContent(string property)
        => CallWithText(NativeMethods.prismpdf_content_begin_af_marked_content, property,
            nameof(property), "prismpdf_content_begin_af_marked_content");

    /// <summary>
    /// Open an artifact marked-content sequence (<c>BMC /Artifact</c>, §14.8.2.2) — content
    /// excluded from the logical structure, which PDF/UA requires for decoration.
    /// </summary>
    public void BeginArtifact()
        => Native.Check(NativeMethods.prismpdf_content_begin_artifact(Handle),
            "prismpdf_content_begin_artifact");

    /// <summary>Close the innermost marked-content sequence (<c>EMC</c>, §14.6).</summary>
    public void EndMarkedContent()
        => Native.Check(NativeMethods.prismpdf_content_end_marked_content(Handle),
            "prismpdf_content_end_marked_content");

    private protected override void Free(nint handle) => NativeMethods.prismpdf_content_free(handle);

    /// <summary>
    /// Lend the assembled bytes without copying them, for a call that reads them immediately.
    /// </summary>
    /// <remarks>
    /// The view dies with this handle, so the caller must keep the instance alive across the
    /// native call it feeds — <c>GC.KeepAlive</c> after the call, not before it.
    /// </remarks>
    internal void BorrowBytes(out byte* data, out nuint len)
    {
        byte* borrowed = null;
        nuint length = 0;
        Native.Check(NativeMethods.prismpdf_content_bytes(Handle, &borrowed, &length),
            "prismpdf_content_bytes");
        data = borrowed;
        len = length;
    }

    private delegate PrismPdfStatus TextCall(nint content, byte* text);

    private void CallWithText(TextCall call, string value, string parameterName, string operation)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var text = Native.AllocUtf8(value);
        try
        {
            Native.Check(call(Handle, text), operation);
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }
}
