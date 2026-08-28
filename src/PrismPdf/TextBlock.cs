using PrismPdf.Collections;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A text style: which font, at what size, with what leading and alignment (§9.4).
/// </summary>
/// <remarks>
/// <para>
/// A style is a value the layout API reads, not something it owns — the same block can be passed to
/// any number of <see cref="Flow"/> calls, and disposed when the last of them has run.
/// </para>
/// <para>
/// It also answers questions about text without drawing any: <see cref="MeasureText(string)"/> and
/// <see cref="WrapText(string, double)"/> use the same metrics the flow does, so measuring here
/// predicts what pouring there will produce.
/// </para>
/// </remarks>
public sealed unsafe class TextBlock : PrismPdfHandle
{
    /// <summary>Create a text style.</summary>
    /// <param name="fontResource">
    /// The name in the page's <c>/Resources /Font</c> — one of the names the flow was created with.
    /// </param>
    /// <param name="baseFont">
    /// The font's PostScript name, used for metrics, e.g. <c>Helvetica-Bold</c>.
    /// </param>
    /// <param name="size">Font size in points.</param>
    /// <param name="leading">Baseline-to-baseline distance in points.</param>
    /// <param name="align">Horizontal alignment.</param>
    public TextBlock(
        string fontResource,
        string baseFont,
        double size,
        double leading,
        Align align = Align.Left)
        : base(NewHandle(fontResource, baseFont, size, leading, align))
    {
    }

    /// <summary>Measure how wide text renders in this style, in points (§9.4).</summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>The advance width in points.</returns>
    public double MeasureText(string text)
    {
        using var strings = new Utf8Scope();
        double width = 0;
        Native.Check(
            NativeMethods.prismpdf_measure_text(Handle, strings.Add(text, nameof(text)), &width),
            "prismpdf_measure_text");
        return width;
    }

    /// <summary>Wrap text to a column width, one string per line.</summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="width">The column width in points.</param>
    /// <returns>An owned list of lines; dispose it when done.</returns>
    public StringList WrapText(string text, double width)
    {
        using var strings = new Utf8Scope();
        nint list = 0;
        Native.Check(
            NativeMethods.prismpdf_wrap_text(Handle, strings.Add(text, nameof(text)), width, &list),
            "prismpdf_wrap_text");
        return new StringList(list);
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_text_block_free(handle);

    private static nint NewHandle(
        string fontResource,
        string baseFont,
        double size,
        double leading,
        Align align)
    {
        using var strings = new Utf8Scope();
        return NativeMethods.prismpdf_text_block_new(
            strings.Add(fontResource, nameof(fontResource)),
            strings.Add(baseFont, nameof(baseFont)),
            size,
            leading,
            align);
    }
}
