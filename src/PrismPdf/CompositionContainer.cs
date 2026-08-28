using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A scoped handle to one slot in a <see cref="Composition"/>'s arena.
/// </summary>
/// <remarks>
/// <para>
/// A container is not a node. It addresses a slot, and the calls below either <em>fill</em> that
/// slot — <c>Set…</c> — or <em>append</em> to a container that is already a column, a row or a
/// table — <c>Add…</c>. Filling spends the handle: the engine advances the slot's generation, so
/// the next use of the same handle raises <see cref="PrismPdfException"/> with
/// <see cref="PrismPdfStatus.InvalidUse"/>. Appending does not, which is why a column handle can
/// be used again and again.
/// </para>
/// <para>
/// Every call that hands back a child returns a new owned handle: dispose it, and dispose it
/// independently of its parent and of the composition. Disposing releases the handle only — the
/// node stays in the composition, which is what makes it correct to write the tree with nested
/// <c>using</c> declarations and still get a complete document out of
/// <see cref="Composition.Build"/>.
/// </para>
/// </remarks>
public sealed unsafe class CompositionContainer : PrismPdfHandle
{
    private readonly Composition _composition;

    internal CompositionContainer(Composition composition, nint handle)
        : base(handle)
    {
        _composition = composition;
    }

    // -----------------------------------------------------------------------------------------
    // Layout containers
    // -----------------------------------------------------------------------------------------

    /// <summary>Fill this slot with a column, stacking its children vertically.</summary>
    /// <param name="spacing">Vertical gap between children, in points.</param>
    /// <returns>A handle used to append the column's children.</returns>
    public CompositionContainer SetColumn(double spacing)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_column(Handle, spacing, &child),
            "prismpdf_composition_container_set_column");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Append an empty child slot to a column.</summary>
    /// <returns>The child's empty slot.</returns>
    public CompositionContainer AddItem()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_column_add_item(Handle, &child),
            "prismpdf_composition_column_add_item");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Fill this slot with a row, placing its children side by side.</summary>
    /// <returns>A handle used to append the row's children.</returns>
    public CompositionContainer SetRow()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_row(Handle, &child),
            "prismpdf_composition_container_set_row");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Append an exact-width child to a row.</summary>
    /// <param name="width">The child's width in points.</param>
    /// <returns>The child's empty slot.</returns>
    public CompositionContainer AddFixed(double width)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_row_add_fixed(Handle, width, &child),
            "prismpdf_composition_row_add_fixed");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Append a child taking a weighted share of the row's remaining width.</summary>
    /// <param name="factor">The weight, relative to the row's other relative children.</param>
    /// <returns>The child's empty slot.</returns>
    public CompositionContainer AddRelative(double factor)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_row_add_relative(Handle, factor, &child),
            "prismpdf_composition_row_add_relative");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Append a naturally sized child to a row.</summary>
    /// <returns>The child's empty slot.</returns>
    public CompositionContainer AddAuto()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_row_add_auto(Handle, &child),
            "prismpdf_composition_row_add_auto");
        return new CompositionContainer(_composition, child);
    }

    // -----------------------------------------------------------------------------------------
    // Tables
    // -----------------------------------------------------------------------------------------

    /// <summary>Fill this slot with a paginating table.</summary>
    /// <returns>The table's editor handle — where columns and rows are added.</returns>
    public CompositionContainer SetTable()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_table(Handle, &child),
            "prismpdf_composition_container_set_table");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Add an exact-width table column.</summary>
    /// <param name="width">The column's width in points.</param>
    public void AddFixedColumn(double width)
        => Native.Check(NativeMethods.prismpdf_composition_table_add_fixed_column(Handle, width),
            "prismpdf_composition_table_add_fixed_column");

    /// <summary>Add a table column taking a weighted share of the remaining width.</summary>
    /// <param name="factor">The weight, relative to the table's other relative columns.</param>
    public void AddRelativeColumn(double factor)
        => Native.Check(NativeMethods.prismpdf_composition_table_add_relative_column(Handle, factor),
            "prismpdf_composition_table_add_relative_column");

    /// <summary>Add a naturally sized table column.</summary>
    public void AddAutoColumn()
        => Native.Check(NativeMethods.prismpdf_composition_table_add_auto_column(Handle),
            "prismpdf_composition_table_add_auto_column");

    /// <summary>Define the table's header row, repeated on each fragment the table breaks into.</summary>
    /// <returns>The header row's handle — where cells are added.</returns>
    public CompositionContainer SetHeader()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_table_set_header(Handle, &child),
            "prismpdf_composition_table_set_header");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Append a table body row.</summary>
    /// <returns>The row's handle — where cells are added.</returns>
    public CompositionContainer AddRow()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_table_add_row(Handle, &child),
            "prismpdf_composition_table_add_row");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Append an empty cell to a table row.</summary>
    /// <returns>The cell's empty slot.</returns>
    public CompositionContainer AddCell()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_table_row_add_cell(Handle, &child),
            "prismpdf_composition_table_row_add_cell");
        return new CompositionContainer(_composition, child);
    }

    // -----------------------------------------------------------------------------------------
    // Leaves
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Fill this slot with wrapping text, using the default Helvetica resource.
    /// </summary>
    /// <remarks>
    /// Inside a page header or footer, <c>{page}</c> and <c>{pages}</c> are substituted once
    /// pagination is known.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="size">Font size in points.</param>
    /// <param name="leading">Baseline-to-baseline spacing in points.</param>
    public void SetText(string text, double size, double leading)
    {
        using var strings = new Utf8Scope();
        var value = strings.Add(text, nameof(text));
        var style = new PrismPdfCompositionTextStyle { Size = size, Leading = leading };

        Native.Check(
            NativeMethods.prismpdf_composition_container_set_text(Handle, value, &style),
            "prismpdf_composition_container_set_text");
    }

    /// <summary>Fill this slot with an image (§8.9).</summary>
    /// <remarks>The image is cloned into the composition, so it may be disposed immediately.</remarks>
    /// <param name="image">The image.</param>
    /// <param name="sizing">How the image is scaled into the box.</param>
    /// <param name="width">The box width in points.</param>
    /// <param name="height">The box height in points.</param>
    public void SetImage(ImageSource image, CompositionImageSizing sizing, double width, double height)
    {
        Throw.IfNull(image);
        Native.Check(
            NativeMethods.prismpdf_composition_container_set_image(
                Handle, image.Handle, sizing, width, height),
            "prismpdf_composition_container_set_image");
        GC.KeepAlive(image);
    }

    /// <summary>Fill this slot with an explicit page break.</summary>
    public void SetPageBreak()
        => Native.Check(NativeMethods.prismpdf_composition_container_set_page_break(Handle),
            "prismpdf_composition_container_set_page_break");

    // -----------------------------------------------------------------------------------------
    // Structure roles
    // -----------------------------------------------------------------------------------------

    /// <summary>Wrap a child in a logical-structure role (§14.7–§14.8).</summary>
    /// <param name="semantic">The role.</param>
    /// <returns>The wrapped child's empty slot.</returns>
    public CompositionContainer SetSemantic(CompositionSemantic semantic)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_semantic(Handle, semantic, &child),
            "prismpdf_composition_container_set_semantic");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Wrap a child in a heading role.</summary>
    /// <param name="level">The heading level, 1 to 6.</param>
    /// <returns>The wrapped child's empty slot.</returns>
    public CompositionContainer SetHeading(int level)
    {
        Throw.IfNegative(level);
        Throw.IfGreaterThan(level, byte.MaxValue);

        nint child = 0;
        Native.Check(
            NativeMethods.prismpdf_composition_container_set_heading(Handle, (byte)level, &child),
            "prismpdf_composition_container_set_heading");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Wrap a child in an accessible URI-link role.</summary>
    /// <param name="uri">The target URI.</param>
    /// <param name="description">
    /// The link's description, which assistive technology reads in place of the URI.
    /// </param>
    /// <returns>The wrapped child's empty slot.</returns>
    public CompositionContainer SetLink(string uri, string description)
    {
        using var strings = new Utf8Scope();
        nint child = 0;
        Native.Check(
            NativeMethods.prismpdf_composition_container_set_link(
                Handle,
                strings.Add(uri, nameof(uri)),
                strings.Add(description, nameof(description)),
                &child),
            "prismpdf_composition_container_set_link");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Wrap a child in a figure role carrying alternate text.</summary>
    /// <param name="alt">What the figure means, in words.</param>
    /// <returns>The wrapped child's empty slot.</returns>
    public CompositionContainer SetFigure(string alt)
    {
        using var strings = new Utf8Scope();
        nint child = 0;
        Native.Check(
            NativeMethods.prismpdf_composition_container_set_figure(
                Handle, strings.Add(alt, nameof(alt)), &child),
            "prismpdf_composition_container_set_figure");
        return new CompositionContainer(_composition, child);
    }

    // -----------------------------------------------------------------------------------------
    // Box decoration and constraints
    // -----------------------------------------------------------------------------------------

    /// <summary>Wrap a child in uniform padding.</summary>
    /// <param name="points">The padding on every side, in points.</param>
    /// <returns>The padded child's empty slot.</returns>
    public CompositionContainer SetPadding(double points)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_padding(Handle, points, &child),
            "prismpdf_composition_container_set_padding");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Wrap a child in an exact-width constraint.</summary>
    /// <param name="points">The width in points.</param>
    /// <returns>The constrained child's empty slot.</returns>
    public CompositionContainer SetWidth(double points)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_width(Handle, points, &child),
            "prismpdf_composition_container_set_width");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Wrap a child in an exact-height constraint.</summary>
    /// <param name="points">The height in points.</param>
    /// <returns>The constrained child's empty slot.</returns>
    public CompositionContainer SetHeight(double points)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_height(Handle, points, &child),
            "prismpdf_composition_container_set_height");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Wrap a child in an alignment constraint.</summary>
    /// <param name="horizontal">Horizontal alignment inside the offered box.</param>
    /// <param name="vertical">Vertical alignment inside the offered box.</param>
    /// <returns>The aligned child's empty slot.</returns>
    public CompositionContainer SetAlignment(
        CompositionHorizontalAlign horizontal,
        CompositionVerticalAlign vertical)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_alignment(Handle, horizontal, vertical, &child),
            "prismpdf_composition_container_set_alignment");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Extend a child to consume all offered width and height.</summary>
    /// <returns>The extended child's empty slot.</returns>
    public CompositionContainer SetExtend()
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_extend(Handle, &child),
            "prismpdf_composition_container_set_extend");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Paint a border around a child.</summary>
    /// <param name="width">Stroke width in points.</param>
    /// <param name="color">The border colour.</param>
    /// <returns>The bordered child's empty slot.</returns>
    public CompositionContainer SetBorder(double width, PdfColor color)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_border(Handle, width, ToNative(color), &child),
            "prismpdf_composition_container_set_border");
        return new CompositionContainer(_composition, child);
    }

    /// <summary>Paint a background behind a child.</summary>
    /// <param name="color">The background colour.</param>
    /// <returns>The child's empty slot.</returns>
    public CompositionContainer SetBackground(PdfColor color)
    {
        nint child = 0;
        Native.Check(NativeMethods.prismpdf_composition_container_set_background(Handle, ToNative(color), &child),
            "prismpdf_composition_container_set_background");
        return new CompositionContainer(_composition, child);
    }

    private protected override void Free(nint handle)
        => NativeMethods.prismpdf_composition_container_free(handle);

    private static PrismPdfCompositionColor ToNative(PdfColor color)
        => new() { Red = color.Red, Green = color.Green, Blue = color.Blue };
}
