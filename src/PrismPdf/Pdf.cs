using System.Runtime.InteropServices;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The library's module-level entry points: everything the ABI exposes whose first parameter is
/// not a handle.
/// </summary>
/// <remarks>
/// Rule 6 of the binding author's guide places these on "the top-level module/class". C# cannot
/// give a type the same name as its enclosing namespace, so the class is <c>Pdf</c> rather than
/// <c>PrismPdf</c>; with <c>using PrismPdf;</c> in scope this reads <c>Pdf.Version</c>. That naming
/// deviation, and every other one, is recorded in <c>docs/naming.md</c>.
/// </remarks>
public static unsafe class Pdf
{
    /// <summary>
    /// The native engine's version, e.g. <c>"0.4.0"</c>.
    /// </summary>
    /// <remarks>
    /// A static string owned by the library; unlike every other string the ABI returns, it must
    /// not be freed. It equals the core release tag without its <c>v</c>: the core's release
    /// workflow refuses to build a tag whose workspace version disagrees.
    /// </remarks>
    public static string Version
    {
        get
        {
            var text = NativeMethods.prismpdf_version();
            return Native.Utf8ToString(text) ?? string.Empty;
        }
    }

    /// <summary>
    /// Concatenate documents, in order, into one new PDF (§7.7.3).
    /// </summary>
    /// <param name="documents">The open documents to merge. None of them is modified.</param>
    /// <returns>The merged PDF's bytes.</returns>
    /// <exception cref="ArgumentException">No documents were supplied.</exception>
    /// <exception cref="PrismPdfException">The merge failed.</exception>
    public static byte[] Merge(params Document[] documents)
        => Merge((IReadOnlyList<Document>)documents);

    /// <inheritdoc cref="Merge(Document[])"/>
    public static byte[] Merge(IReadOnlyList<Document> documents)
    {
        var handles = CollectHandles(documents);

        fixed (nint* docs = handles)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_merge(docs, (nuint)handles.Length, &data, &len),
                "prismpdf_merge");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>
    /// Concatenate documents and report what the merge did to signatures and logical structure.
    /// </summary>
    /// <remarks>
    /// Rule 7 of the binding author's guide keeps the report variant a separate method rather than
    /// an optional parameter, so <see cref="Merge(Document[])"/> stays allocation-free of reports.
    /// </remarks>
    /// <param name="documents">The open documents to merge. None of them is modified.</param>
    /// <returns>An owned report; dispose it when done.</returns>
    public static TransformReport MergeWithReport(IReadOnlyList<Document> documents)
    {
        var handles = CollectHandles(documents);

        fixed (nint* docs = handles)
        {
            nint report = 0;
            Native.Check(NativeMethods.prismpdf_merge_report(docs, (nuint)handles.Length, &report),
                "prismpdf_merge_report");
            return new TransformReport(report);
        }
    }

    /// <summary>The ISO 19005 part number of a conformance level: 1, 2, 3 or 4.</summary>
    /// <param name="conformance">The level.</param>
    /// <returns>The part number.</returns>
    public static int PdfAPart(PdfAConformance conformance)
        => NativeMethods.prismpdf_pdfa_part(conformance);

    /// <summary>
    /// Whether a level permits embedded files — only PDF/A-3 and PDF/A-4f do (§6.8).
    /// </summary>
    /// <remarks>
    /// Check this before attaching a file to a document destined for PDF/A: the conformance pass
    /// would otherwise refuse it with <see cref="ConformanceIssue.AttachmentRequiresPdfA3"/>.
    /// </remarks>
    /// <param name="conformance">The level.</param>
    /// <returns><see langword="true"/> when attachments are permitted.</returns>
    public static bool PdfAAllowsAttachments(PdfAConformance conformance)
        => Native.ToBool(NativeMethods.prismpdf_pdfa_allows_attachments(conformance));

    /// <summary>The conformance code as it appears in XMP, e.g. <c>2u</c>.</summary>
    /// <param name="conformance">The level.</param>
    /// <returns>The code.</returns>
    public static string PdfACode(PdfAConformance conformance)
    {
        byte* text = null;
        Native.Check(NativeMethods.prismpdf_pdfa_code(conformance, &text), "prismpdf_pdfa_code");
        return Native.TakeString(text) ?? string.Empty;
    }

    private static nint[] CollectHandles(IReadOnlyList<Document> documents)
    {
        Throw.IfNull(documents);
        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one document is required.", nameof(documents));
        }

        var handles = new nint[documents.Count];
        for (var i = 0; i < documents.Count; i++)
        {
            handles[i] = documents[i]?.Handle
                ?? throw new ArgumentException($"Document at index {i} is null.", nameof(documents));
        }

        return handles;
    }
}
