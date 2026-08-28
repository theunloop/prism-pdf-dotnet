using PrismPdf.Interop;

namespace PrismPdf;

// COS inspection: the document as its object graph (§7.3) rather than as pages and annotations.
// Every reader here clones, so an object outlives the document it came from and disposing one is
// never visible to the other.
public sealed unsafe partial class Document
{
    /// <summary>The document catalog (§7.7.2), cloned as an owned object.</summary>
    /// <returns>An owned object; dispose it when done.</returns>
    public PdfObject CatalogObject()
    {
        nint value = 0;
        Native.Check(NativeMethods.prismpdf_document_catalog_object(Handle, &value),
            "prismpdf_document_catalog_object");
        return new PdfObject(value);
    }

    /// <summary>
    /// One page's dictionary (§7.7.3.4), cloned as an owned object with inherited attributes
    /// already resolved.
    /// </summary>
    /// <param name="index">Zero-based page index.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public PdfObject PageObject(int index)
    {
        nint value = 0;
        Native.Check(
            NativeMethods.prismpdf_document_page_object(
                Handle, Native.ToIndex(index, nameof(index)), &value),
            "prismpdf_document_page_object");
        return new PdfObject(value);
    }

    /// <summary>Fetch an indirect object by identity (§7.3.10).</summary>
    /// <remarks>
    /// A missing or free object comes back as the null object rather than as an error, which is
    /// what §7.3.10 says a reference to a free object means.
    /// </remarks>
    /// <param name="reference">The object and generation numbers.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public PdfObject GetObject(PdfReference reference)
    {
        nint value = 0;
        Native.Check(
            NativeMethods.prismpdf_document_get_object(
                Handle, reference.ToNumber(), reference.ToGeneration(), &value),
            "prismpdf_document_get_object");
        return new PdfObject(value);
    }

    /// <summary>
    /// Follow an indirect-reference chain to the direct object at its end (§7.3.10).
    /// </summary>
    /// <param name="reference">
    /// The object to resolve. A direct object resolves to a copy of itself, so this is safe to
    /// call without checking <see cref="PdfObject.Kind"/> first.
    /// </param>
    /// <returns>An owned direct object; dispose it when done.</returns>
    public PdfObject ResolveObject(PdfObject reference)
    {
        Throw.IfNull(reference);

        nint value = 0;
        Native.Check(
            NativeMethods.prismpdf_document_resolve_object(Handle, reference.Handle, &value),
            "prismpdf_document_resolve_object");
        GC.KeepAlive(reference);
        return new PdfObject(value);
    }
}
