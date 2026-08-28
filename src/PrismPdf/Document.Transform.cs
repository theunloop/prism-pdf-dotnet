using PrismPdf.Interop;

namespace PrismPdf;

// The write and transform path. Every method here serialises a NEW document and leaves this handle
// untouched — the boundary is immutable, so a binding never has to reason about aliasing.
//
// Each transform comes in two forms, per rule 7 of the binding author's guide: the plain call
// returning bytes, and a `…WithReport` companion returning a TransformReport. They are separate
// methods rather than one signature with an optional parameter, so the cheap path stays
// report-free.
public sealed unsafe partial class Document
{
    /// <summary>Full rewrite with a classic cross-reference table (§7.5.4); normalises and repairs.</summary>
    /// <returns>The serialized PDF.</returns>
    public byte[] Save() => EmitBytes(NativeMethods.prismpdf_document_save, "prismpdf_document_save");

    /// <inheritdoc cref="Save()"/>
    /// <returns>An owned report carrying the bytes and what the save cost.</returns>
    public TransformReport SaveWithReport()
        => EmitReport(NativeMethods.prismpdf_document_save_report, "prismpdf_document_save_report");

    /// <summary>Full rewrite with a cross-reference <em>stream</em> (§7.5.8, PDF 1.5+).</summary>
    /// <returns>The serialized PDF.</returns>
    public byte[] SaveCompact()
        => EmitBytes(NativeMethods.prismpdf_document_save_compact, "prismpdf_document_save_compact");

    /// <inheritdoc cref="SaveCompact()"/>
    /// <returns>An owned report carrying the bytes and what the save cost.</returns>
    public TransformReport SaveCompactWithReport()
        => EmitReport(NativeMethods.prismpdf_document_save_compact_report,
            "prismpdf_document_save_compact_report");

    /// <summary>Full rewrite using object streams (§7.5.7) — the smallest of the three save modes.</summary>
    /// <returns>The serialized PDF.</returns>
    public byte[] SavePacked()
        => EmitBytes(NativeMethods.prismpdf_document_save_packed, "prismpdf_document_save_packed");

    /// <inheritdoc cref="SavePacked()"/>
    /// <returns>An owned report carrying the bytes and what the save cost.</returns>
    public TransformReport SavePackedWithReport()
        => EmitReport(NativeMethods.prismpdf_document_save_packed_report,
            "prismpdf_document_save_packed_report");

    /// <summary>
    /// Full rewrite at an exact header version. Constructs above the target are <em>refused</em>,
    /// not silently downgraded.
    /// </summary>
    /// <param name="major">Target major version.</param>
    /// <param name="minor">Target minor version.</param>
    /// <returns>The serialized PDF.</returns>
    public byte[] SaveAs(int major, int minor)
    {
        byte* data = null;
        nuint len = 0;
        Native.Check(
            NativeMethods.prismpdf_document_save_as(Handle, ToVersionPart(major, nameof(major)),
                ToVersionPart(minor, nameof(minor)), &data, &len),
            "prismpdf_document_save_as");
        return Native.TakeBytes(data, len);
    }

    /// <inheritdoc cref="SaveAs(int, int)"/>
    /// <param name="major">Target major version.</param>
    /// <param name="minor">Target minor version.</param>
    /// <returns>An owned report carrying the bytes and what the save cost.</returns>
    public TransformReport SaveAsWithReport(int major, int minor)
    {
        nint report = 0;
        Native.Check(
            NativeMethods.prismpdf_document_save_as_report(Handle, ToVersionPart(major, nameof(major)),
                ToVersionPart(minor, nameof(minor)), &report),
            "prismpdf_document_save_as_report");
        return new TransformReport(report);
    }

    /// <summary>
    /// A new document containing only the given pages, in the given order (§7.7.3) — split, subset
    /// and reorder in one call. Duplicates are allowed.
    /// </summary>
    /// <param name="indices">Zero-based page indices.</param>
    /// <returns>The serialized PDF.</returns>
    public byte[] ExtractPages(IReadOnlyList<int> indices)
    {
        var pages = ToIndices(indices);
        fixed (nuint* items = pages)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_extract_pages(Handle, items, (nuint)pages.Length, &data, &len),
                "prismpdf_document_extract_pages");
            return Native.TakeBytes(data, len);
        }
    }

    /// <inheritdoc cref="ExtractPages(IReadOnlyList{int})"/>
    /// <param name="indices">Zero-based page indices.</param>
    /// <returns>An owned report carrying the bytes and what the extraction cost.</returns>
    public TransformReport ExtractPagesWithReport(IReadOnlyList<int> indices)
    {
        var pages = ToIndices(indices);
        fixed (nuint* items = pages)
        {
            nint report = 0;
            Native.Check(
                NativeMethods.prismpdf_document_extract_pages_report(Handle, items, (nuint)pages.Length, &report),
                "prismpdf_document_extract_pages_report");
            return new TransformReport(report);
        }
    }

    /// <summary>A new document with one page rotated (§7.7.3.3).</summary>
    /// <param name="index">Zero-based page index.</param>
    /// <param name="degrees">Rotation in degrees; must be a multiple of 90.</param>
    /// <returns>The serialized PDF.</returns>
    public byte[] RotatePage(int index, long degrees)
    {
        byte* data = null;
        nuint len = 0;
        Native.Check(
            NativeMethods.prismpdf_document_rotate_page(
                Handle, Native.ToIndex(index, nameof(index)), degrees, &data, &len),
            "prismpdf_document_rotate_page");
        return Native.TakeBytes(data, len);
    }

    /// <inheritdoc cref="RotatePage(int, long)"/>
    /// <param name="index">Zero-based page index.</param>
    /// <param name="degrees">Rotation in degrees; must be a multiple of 90.</param>
    /// <returns>An owned report carrying the bytes and what the rotation cost.</returns>
    public TransformReport RotatePageWithReport(int index, long degrees)
    {
        nint report = 0;
        Native.Check(
            NativeMethods.prismpdf_document_rotate_page_report(
                Handle, Native.ToIndex(index, nameof(index)), degrees, &report),
            "prismpdf_document_rotate_page_report");
        return new TransformReport(report);
    }

    /// <summary>Subset every embedded font to the glyphs the document actually uses.</summary>
    /// <returns>The serialized PDF.</returns>
    public byte[] SubsetFonts()
        => EmitBytes(NativeMethods.prismpdf_document_subset_fonts, "prismpdf_document_subset_fonts");

    /// <inheritdoc cref="SubsetFonts()"/>
    /// <returns>An owned report carrying the bytes and what the subsetting cost.</returns>
    public TransformReport SubsetFontsWithReport()
        => EmitReport(NativeMethods.prismpdf_document_subset_fonts_report,
            "prismpdf_document_subset_fonts_report");

    /// <summary>
    /// Fill form fields by name and re-emit as an incremental update (§7.5.6). Unknown names are
    /// ignored.
    /// </summary>
    /// <param name="values">Field name to value. Match names against <see cref="Collections.FormField.Name"/>.</param>
    /// <returns>The serialized PDF.</returns>
    public byte[] FillForm(IReadOnlyDictionary<string, string> values)
    {
        var (names, texts) = SplitPairs(values);
        using var nameArray = new NativeUtf8Array(names, nameof(values));
        using var valueArray = new NativeUtf8Array(texts, nameof(values));

        byte* data = null;
        nuint len = 0;
        Native.Check(
            NativeMethods.prismpdf_document_fill_form(
                Handle, nameArray.Pointers, valueArray.Pointers, nameArray.Count, &data, &len),
            "prismpdf_document_fill_form");
        return Native.TakeBytes(data, len);
    }

    /// <inheritdoc cref="FillForm(IReadOnlyDictionary{string, string})"/>
    /// <param name="values">Field name to value.</param>
    /// <returns>An owned report carrying the bytes and what the fill cost.</returns>
    public TransformReport FillFormWithReport(IReadOnlyDictionary<string, string> values)
    {
        var (names, texts) = SplitPairs(values);
        using var nameArray = new NativeUtf8Array(names, nameof(values));
        using var valueArray = new NativeUtf8Array(texts, nameof(values));

        nint report = 0;
        Native.Check(
            NativeMethods.prismpdf_document_fill_form_report(
                Handle, nameArray.Pointers, valueArray.Pointers, nameArray.Count, &report),
            "prismpdf_document_fill_form_report");
        return new TransformReport(report);
    }

    /// <summary>
    /// Stamp widget appearances into the page content, drop <c>/AcroForm</c>, and return the
    /// rewritten PDF — the form's values become ordinary, uneditable page content.
    /// </summary>
    /// <returns>The serialized PDF.</returns>
    public byte[] FlattenForm()
        => EmitBytes(NativeMethods.prismpdf_document_flatten_form, "prismpdf_document_flatten_form");

    /// <inheritdoc cref="FlattenForm()"/>
    /// <returns>An owned report carrying the bytes and what the flattening cost.</returns>
    public TransformReport FlattenFormWithReport()
        => EmitReport(NativeMethods.prismpdf_document_flatten_form_report,
            "prismpdf_document_flatten_form_report");

    private delegate PrismPdfStatus BytesCall(nint doc, byte** outData, nuint* outLen);

    private delegate PrismPdfStatus ReportCall(nint doc, nint* outReport);

    private static byte ToVersionPart(int value, string parameterName)
    {
        Throw.IfNegative(value, parameterName);
        Throw.IfGreaterThan(value, byte.MaxValue, parameterName);
        return (byte)value;
    }

    private static nuint[] ToIndices(IReadOnlyList<int> indices)
    {
        Throw.IfNull(indices);
        if (indices.Count == 0)
        {
            throw new ArgumentException("At least one page index is required.", nameof(indices));
        }

        var pages = new nuint[indices.Count];
        for (var i = 0; i < indices.Count; i++)
        {
            pages[i] = Native.ToIndex(indices[i], nameof(indices));
        }

        return pages;
    }

    private static (List<string> Names, List<string> Values) SplitPairs(
        IReadOnlyDictionary<string, string> values)
    {
        Throw.IfNull(values);

        var names = new List<string>(values.Count);
        var texts = new List<string>(values.Count);
        foreach (var pair in values)
        {
            names.Add(pair.Key);
            texts.Add(pair.Value);
        }

        return (names, texts);
    }

    private byte[] EmitBytes(BytesCall call, string operation)
    {
        byte* data = null;
        nuint len = 0;
        Native.Check(call(Handle, &data, &len), operation);
        return Native.TakeBytes(data, len);
    }

    private TransformReport EmitReport(ReportCall call, string operation)
    {
        nint report = 0;
        Native.Check(call(Handle, &report), operation);
        return new TransformReport(report);
    }
}
