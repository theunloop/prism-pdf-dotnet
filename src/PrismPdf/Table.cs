using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A table of text cells, with fixed column widths, ready to be poured into a <see cref="Flow"/>.
/// </summary>
/// <remarks>
/// The flow breaks the table across pages and repeats the header row where
/// <see cref="HeaderRow"/> is set. Like <see cref="TextBlock"/>, a table is read rather than
/// consumed by <see cref="Flow.AddTable(Table)"/>: place the same one twice if that is what the
/// document needs.
/// </remarks>
public sealed unsafe class Table : PrismPdfHandle
{
    /// <summary>Create a table with the given column widths, in points.</summary>
    /// <param name="columnWidths">One width per column, in column order.</param>
    public Table(ReadOnlySpan<double> columnWidths)
        : base(NewHandle(columnWidths))
    {
    }

    /// <summary>Font size in points.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public double Size
    {
        set => Native.Check(NativeMethods.prismpdf_table_set_size(Handle, value),
            "prismpdf_table_set_size");
    }

    /// <summary>Baseline-to-baseline distance in points.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public double Leading
    {
        set => Native.Check(NativeMethods.prismpdf_table_set_leading(Handle, value),
            "prismpdf_table_set_leading");
    }

    /// <summary>Cell padding in points.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public double Padding
    {
        set => Native.Check(NativeMethods.prismpdf_table_set_padding(Handle, value),
            "prismpdf_table_set_padding");
    }

    /// <summary>Border stroke width in points; zero draws none.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public double Border
    {
        set => Native.Check(NativeMethods.prismpdf_table_set_border(Handle, value),
            "prismpdf_table_set_border");
    }

    /// <summary>Whether the first row is a header, repeated on each page.</summary>
    /// <remarks>Set this <em>before</em> adding rows. Write-only: the ABI has no getter.</remarks>
    public bool HeaderRow
    {
        set => Native.Check(
            NativeMethods.prismpdf_table_set_header_row(Handle, Native.FromBool(value)),
            "prismpdf_table_set_header_row");
    }

    /// <summary>Horizontal alignment of every cell.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public Align Align
    {
        set => Native.Check(NativeMethods.prismpdf_table_set_align(Handle, value),
            "prismpdf_table_set_align");
    }

    /// <summary>Set the table's font.</summary>
    /// <param name="resource">The page resource name.</param>
    /// <param name="baseFont">The PostScript base font used for metrics.</param>
    public void SetFont(string resource, string baseFont)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_table_set_font(
                Handle, strings.Add(resource, nameof(resource)), strings.Add(baseFont, nameof(baseFont))),
            "prismpdf_table_set_font");
    }

    /// <summary>Append a row of cells, in column order.</summary>
    /// <param name="cells">One string per column.</param>
    public void AddRow(params string[] cells) => AddRow((IReadOnlyList<string>)cells);

    /// <inheritdoc cref="AddRow(string[])"/>
    public void AddRow(IReadOnlyList<string> cells)
    {
        using var values = new NativeUtf8Array(cells, nameof(cells));
        Native.Check(NativeMethods.prismpdf_table_add_row(Handle, values.Pointers, values.Count),
            "prismpdf_table_add_row");
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_table_free(handle);

    private static nint NewHandle(ReadOnlySpan<double> columnWidths)
    {
        fixed (double* columns = columnWidths)
        {
            return NativeMethods.prismpdf_table_new(columns, (nuint)columnWidths.Length);
        }
    }
}
