using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// An object-edit transaction: replace indirect objects in a document and write the result out
/// (§7.5).
/// </summary>
/// <remarks>
/// <para>
/// This is the write half of the COS escape hatch. Changes are collected against the document they
/// came from and applied all at once by <see cref="Commit(EditCommitMode)"/>, which produces new
/// bytes — the source document is not modified, as everywhere else at this boundary.
/// </para>
/// <para>
/// The commit mode matters more than it looks.
/// <see cref="EditCommitMode.Incremental"/> appends a revision and keeps every original byte, so a
/// signature covering the original range still verifies;
/// <see cref="EditCommitMode.FullRewrite"/> re-emits the object graph, which normalises the file
/// and invalidates those signatures. The returned <see cref="TransformReport"/> says which effect
/// the commit actually had.
/// </para>
/// <para>
/// <b>Committing consumes the edit</b> on success; a failure leaves it owned and usable. The
/// document must stay alive for the whole transaction — this wrapper holds a reference to it, so
/// the collector cannot take it early.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var doc = Document.Open(bytes);
/// using var catalog = doc.CatalogObject();
/// using var edit = new Edit(doc);
///
/// using var marker = PdfObject.NewName("Marked");
/// catalog.DictionarySet("Custom", marker);
/// edit.SetObject(rootReference, catalog);
///
/// using var report = edit.Commit(EditCommitMode.Incremental);
/// var updated = report.Bytes;
/// </code>
/// </example>
public sealed unsafe class Edit : PrismPdfHandle
{
    private readonly Document _document;

    /// <summary>Begin a transaction against a document.</summary>
    /// <param name="document">The document to edit. Not modified.</param>
    public Edit(Document document)
        : base(NewHandle(document))
    {
        _document = document;
    }

    /// <summary>
    /// Add or replace one changed indirect object (§7.3.10). The value is cloned; setting the same
    /// identity again replaces the earlier change.
    /// </summary>
    /// <param name="reference">Which indirect object to replace.</param>
    /// <param name="value">The new value. Cloned; stays owned by the caller.</param>
    public void SetObject(PdfReference reference, PdfObject value)
    {
        Throw.IfNull(value);

        Native.Check(
            NativeMethods.prismpdf_edit_set_object(
                Handle, reference.ToNumber(), reference.ToGeneration(), value.Handle),
            "prismpdf_edit_set_object");
        GC.KeepAlive(value);
    }

    /// <summary>
    /// Apply every change and serialise the result. <b>Consuming:</b> on success this edit is
    /// spent and its wrapper is invalid; a failure leaves it usable.
    /// </summary>
    /// <param name="mode">Append a revision, or re-emit the whole graph.</param>
    /// <returns>An owned report carrying the bytes and what the commit cost.</returns>
    public TransformReport Commit(EditCommitMode mode = EditCommitMode.Incremental)
    {
        nint report = 0;
        var status = NativeMethods.prismpdf_edit_commit(Handle, _document.Handle, mode, &report);
        if (status == PrismPdfStatus.Ok)
        {
            MarkConsumed();
        }

        Native.Check(status, "prismpdf_edit_commit");
        GC.KeepAlive(_document);
        return new TransformReport(report);
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_edit_free(handle);

    private static nint NewHandle(Document document)
    {
        Throw.IfNull(document);
        return NativeMethods.prismpdf_edit_new(document.Handle);
    }
}
