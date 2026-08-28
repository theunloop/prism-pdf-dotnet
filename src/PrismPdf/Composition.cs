using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// Declarative composition: describe the document as a tree of boxes and let the engine paginate
/// it (§14.8).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Flow"/> pours content in reading order; a composition instead describes structure —
/// columns inside rows inside padding inside a page — and the engine measures, breaks and paints
/// it. It is the richest of the three authoring APIs, and the one that produces tagged output most
/// directly: <see cref="TaggedLanguage"/> plus <see cref="CompositionContainer.SetSemantic"/> is a
/// structure tree without a single marked-content identifier written by hand.
/// </para>
/// <para>
/// <b>The arena model.</b> The composition owns every node. A <see cref="CompositionContainer"/> is
/// not a node — it is a <em>scoped handle</em> addressing one empty slot, carrying the arena's
/// identity, the slot's id, and a generation. Filling a slot advances that generation, so the
/// handle that filled it is spent: using it again raises <see cref="PrismPdfException"/> with
/// <see cref="PrismPdfStatus.InvalidUse"/>. Disposing a container releases only the handle; the
/// node it filled stays in the composition. Disposing the composition invalidates every surviving
/// container without leaving one pointing at freed memory, and they stay safe to dispose.
/// </para>
/// <para>
/// This SDK deliberately does <em>not</em> mirror those generations in managed state. The engine
/// is the only thing that knows which calls consume a generation and which do not — appending to a
/// column does not, filling a slot does — so duplicating the rule here would be a second
/// implementation free to drift from the first. A spent handle therefore reports
/// <c>InvalidUse</c> from the library, not <see cref="ObjectDisposedException"/> from managed code.
/// The one thing the managed layer does add is a reference from each container back to its
/// composition, so the collector cannot finalise the arena while a handle into it is still
/// reachable.
/// </para>
/// <para>
/// <see cref="Build"/> is one-way: the composition becomes immutable afterwards, whether layout
/// succeeded or failed. It does not release the handle — dispose it as usual.
/// </para>
/// <para>
/// Not thread-safe, per semantic contract 6: confine an instance and its containers to one thread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var composition = new Composition { TaggedLanguage = "en-GB" };
/// using var page = composition.AddPage(PdfSize.A4, PdfMargins.Uniform(48));
/// using var column = page.SetColumn(12);
/// using var heading = column.AddItem();
/// using var headingText = heading.SetHeading(1);
/// headingText.SetText("Invoice", 20, 24);
/// var pdf = composition.Build();
/// </code>
/// </example>
public sealed unsafe class Composition : PrismPdfHandle
{
    /// <summary>Create an empty composition.</summary>
    public Composition()
        : base(NativeMethods.prismpdf_composition_new())
    {
    }

    /// <summary>
    /// Enable tagged-PDF output and set the document language, e.g. <c>en-GB</c>.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string TaggedLanguage
    {
        set
        {
            Throw.IfNull(value);
            using var strings = new Utf8Scope();
            Native.Check(
                NativeMethods.prismpdf_composition_set_tagged_language(
                    Handle, strings.Add(value, nameof(value))),
                "prismpdf_composition_set_tagged_language");
        }
    }

    /// <summary>Add a page and return the handle to its empty content slot.</summary>
    /// <param name="size">The page size in points.</param>
    /// <param name="margins">The content margins in points.</param>
    /// <returns>An owned handle to the page's content slot; dispose it when done.</returns>
    public CompositionContainer AddPage(PdfSize size, PdfMargins margins)
    {
        var style = new PrismPdfCompositionPageStyle
        {
            Width = size.Width,
            Height = size.Height,
            MarginLeft = margins.Left,
            MarginRight = margins.Right,
            MarginTop = margins.Top,
            MarginBottom = margins.Bottom,
        };

        nint container = 0;
        Native.Check(NativeMethods.prismpdf_composition_add_page(Handle, &style, &container),
            "prismpdf_composition_add_page");
        return new CompositionContainer(this, container);
    }

    /// <summary>
    /// Add a header tree repeated on every physical page a page design produces.
    /// </summary>
    /// <remarks>
    /// Text inside a header or footer may contain the <c>{page}</c> and <c>{pages}</c>
    /// placeholders, which the engine substitutes once pagination is known.
    /// </remarks>
    /// <param name="pageIndex">Zero-based index of the page design.</param>
    /// <returns>An owned handle to the header's empty slot; dispose it when done.</returns>
    public CompositionContainer SetPageHeader(int pageIndex)
    {
        nint container = 0;
        Native.Check(
            NativeMethods.prismpdf_composition_page_set_header(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), &container),
            "prismpdf_composition_page_set_header");
        return new CompositionContainer(this, container);
    }

    /// <inheritdoc cref="SetPageHeader(int)"/>
    /// <param name="pageIndex">Zero-based index of the page design.</param>
    /// <returns>An owned handle to the footer's empty slot; dispose it when done.</returns>
    public CompositionContainer SetPageFooter(int pageIndex)
    {
        nint container = 0;
        Native.Check(
            NativeMethods.prismpdf_composition_page_set_footer(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), &container),
            "prismpdf_composition_page_set_footer");
        return new CompositionContainer(this, container);
    }

    /// <summary>
    /// Lay the tree out and serialise it. One-way: the composition is immutable afterwards, even
    /// when layout failed.
    /// </summary>
    /// <returns>The serialized PDF.</returns>
    /// <exception cref="PrismPdfException">
    /// Layout could not place the tree — <see cref="PrismPdfStatus.Layout"/>.
    /// </exception>
    public byte[] Build()
    {
        byte* data = null;
        nuint len = 0;
        Native.Check(NativeMethods.prismpdf_composition_build(Handle, &data, &len),
            "prismpdf_composition_build");
        return Native.TakeBytes(data, len);
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_composition_free(handle);
}
