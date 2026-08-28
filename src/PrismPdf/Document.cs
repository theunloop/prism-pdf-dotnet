using System.Text;
using PrismPdf.Collections;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// An opened PDF: the owner of the byte source and the cross-reference table, and the only thing
/// that can resolve a reference into the object it points to.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>Page</c> type</b>, and that is deliberate. The ABI has no page handle;
/// page-indexed calls such as <see cref="PageText(int)"/> and <see cref="PageAnnotations(int)"/>
/// are methods on the document taking a page index. A binding that grows a page façade diverges
/// from every other Prism PDF binding for no capability gain.
/// </para>
/// <para>
/// <b>The boundary is immutable.</b> Every transform serialises a <em>new</em> document and leaves
/// this one untouched, so there is never any aliasing to reason about: <c>doc.Save()</c> returns
/// bytes, it does not write to <c>doc</c>.
/// </para>
/// <para>
/// Dispose the document when done. The ABI makes no thread-safety promise for a handle: confine an
/// instance to one thread, or guard it with your own lock.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var doc = Document.OpenFile("input.pdf");
/// Console.WriteLine($"{doc.PageCount} pages");
/// Console.WriteLine(doc.Text);
/// File.WriteAllBytes("output.pdf", doc.Save());
/// </code>
/// </example>
public sealed unsafe partial class Document : PrismPdfHandle
{
    private Document(nint handle)
        : base(handle)
    {
    }

    // -------------------------------------------------------------------------------------------
    // Opening
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Open a PDF from memory, recovering automatically if the cross-reference is broken.
    /// </summary>
    /// <param name="data">The file's bytes. They are copied; the caller's buffer is not retained.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="PrismPdfException">
    /// <see cref="PrismPdfStatus.Parse"/> when the file could not be read even after recovery, or
    /// <see cref="PrismPdfStatus.Password"/> when it is encrypted and no password was supplied.
    /// </exception>
    public static Document Open(ReadOnlySpan<byte> data)
    {
        fixed (byte* bytes = data)
        {
            nint handle = 0;
            Native.Check(NativeMethods.prismpdf_document_open(bytes, (nuint)data.Length, &handle),
                "prismpdf_document_open");
            return new Document(handle);
        }
    }

    /// <summary>Open an encrypted PDF (§7.6). The password is tried as both user and owner password.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="password">The password, encoded as UTF-8.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="PrismPdfException"><see cref="PrismPdfStatus.Password"/> when it is wrong.</exception>
    public static Document Open(ReadOnlySpan<byte> data, string password)
    {
        Throw.IfNull(password);
        return Open(data, Encoding.UTF8.GetBytes(password));
    }

    /// <inheritdoc cref="Open(ReadOnlySpan{byte}, string)"/>
    public static Document Open(ReadOnlySpan<byte> data, ReadOnlySpan<byte> password)
    {
        fixed (byte* bytes = data)
        fixed (byte* pass = password)
        {
            nint handle = 0;
            Native.Check(
                NativeMethods.prismpdf_document_open_with_password(
                    bytes, (nuint)data.Length, pass, (nuint)password.Length, &handle),
                "prismpdf_document_open_with_password");
            return new Document(handle);
        }
    }

    /// <summary>Open a PDF under explicit anti-DoS limits and/or a password.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="options">Reusable options; the document does not borrow the handle.</param>
    /// <returns>The opened document.</returns>
    public static Document Open(ReadOnlySpan<byte> data, OpenOptions options)
    {
        Throw.IfNull(options);

        fixed (byte* bytes = data)
        {
            nint handle = 0;
            Native.Check(
                NativeMethods.prismpdf_document_open_with_options(
                    bytes, (nuint)data.Length, options.Handle, &handle),
                "prismpdf_document_open_with_options");
            return new Document(handle);
        }
    }

    /// <summary>Open a certificate-encrypted PDF (§7.6.5).</summary>
    /// <param name="data">The file's bytes.</param>
    /// <param name="certificateDer">The recipient certificate, DER-encoded.</param>
    /// <param name="privateKeyDer">The matching private key, DER-encoded.</param>
    /// <returns>The opened document.</returns>
    public static Document OpenWithPrivateKey(
        ReadOnlySpan<byte> data, ReadOnlySpan<byte> certificateDer, ReadOnlySpan<byte> privateKeyDer)
    {
        fixed (byte* bytes = data)
        fixed (byte* cert = certificateDer)
        fixed (byte* key = privateKeyDer)
        {
            nint handle = 0;
            Native.Check(
                NativeMethods.prismpdf_document_open_with_private_key(
                    bytes, (nuint)data.Length, cert, (nuint)certificateDer.Length,
                    key, (nuint)privateKeyDer.Length, &handle),
                "prismpdf_document_open_with_private_key");
            return new Document(handle);
        }
    }

    /// <summary>Read a file from disk and open it.</summary>
    /// <remarks>
    /// A .NET convenience over <see cref="Open(ReadOnlySpan{byte})"/>, not an ABI entry point —
    /// the engine only ever opens from memory.
    /// </remarks>
    /// <param name="path">Path to the PDF.</param>
    /// <returns>The opened document.</returns>
    public static Document OpenFile(string path) => Open(File.ReadAllBytes(path));

    /// <inheritdoc cref="OpenFile(string)"/>
    /// <param name="path">Path to the PDF.</param>
    /// <param name="password">The password, encoded as UTF-8.</param>
    public static Document OpenFile(string path, string password)
        => Open(File.ReadAllBytes(path), password);

    /// <inheritdoc cref="OpenFile(string)"/>
    /// <param name="path">Path to the PDF.</param>
    /// <param name="options">Reusable open options.</param>
    public static Document OpenFile(string path, OpenOptions options)
        => Open(File.ReadAllBytes(path), options);

    // -------------------------------------------------------------------------------------------
    // Reading
    // -------------------------------------------------------------------------------------------

    /// <summary>The number of pages.</summary>
    public int PageCount
    {
        get
        {
            nuint count = 0;
            Native.Check(NativeMethods.prismpdf_document_page_count(Handle, &count),
                "prismpdf_document_page_count");
            return Native.ToCount(count);
        }
    }

    /// <summary>
    /// The version declared in the file header, or <see langword="null"/> when the file declares
    /// none.
    /// </summary>
    public PdfVersion? Version
    {
        get
        {
            byte major = 0;
            byte minor = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_document_version(Handle, &major, &minor),
                "prismpdf_document_version")
                ? new PdfVersion(major, minor)
                : null;
        }
    }

    /// <summary>
    /// The minimum version the content actually requires, which can be <em>below</em> the declared
    /// header version.
    /// </summary>
    public PdfVersion? MinimumVersion
    {
        get
        {
            byte major = 0;
            byte minor = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_document_min_version(Handle, &major, &minor),
                "prismpdf_document_min_version")
                ? new PdfVersion(major, minor)
                : null;
        }
    }

    /// <summary>
    /// The whole document's text in reading order, decoded through each font's <c>/ToUnicode</c>
    /// where present.
    /// </summary>
    /// <remarks>Extraction runs on every read; cache the result if you need it more than once.</remarks>
    public string Text
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_document_text(Handle, &text), "prismpdf_document_text");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary>The XMP metadata packet (§14.3.2) as raw XML, or <see langword="null"/> when absent.</summary>
    public string? Xmp
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_document_xmp(Handle, &text),
                "prismpdf_document_xmp")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <summary><c>/CreationDate</c>, or <see langword="null"/> when absent or unparseable.</summary>
    public PdfDate? CreationDate => ReadDate(NativeMethods.prismpdf_document_creation_date,
        "prismpdf_document_creation_date");

    /// <summary><c>/ModDate</c>, or <see langword="null"/> when absent or unparseable.</summary>
    public PdfDate? ModificationDate => ReadDate(NativeMethods.prismpdf_document_modification_date,
        "prismpdf_document_modification_date");

    /// <summary>
    /// Extract one page's text in reading order.
    /// </summary>
    /// <param name="index">Zero-based page index.</param>
    /// <returns>The page's text.</returns>
    /// <exception cref="PrismPdfException">
    /// <see cref="PrismPdfStatus.NotFound"/> when <paramref name="index"/> is past the last page.
    /// Unlike an optional getter, an out-of-range index is an error, not absence.
    /// </exception>
    public string PageText(int index)
    {
        byte* text = null;
        Native.Check(NativeMethods.prismpdf_page_text(Handle, Native.ToIndex(index, nameof(index)), &text),
            "prismpdf_page_text");
        return Native.TakeString(text) ?? string.Empty;
    }

    /// <summary>
    /// Extract one page's text with layout preserved — line breaks and gaps derived from the text
    /// matrix, rather than the reading-order run <see cref="PageText(int)"/> returns.
    /// </summary>
    /// <param name="index">Zero-based page index.</param>
    /// <returns>The page's text, positioned.</returns>
    public string PageTextPositioned(int index)
    {
        byte* text = null;
        Native.Check(
            NativeMethods.prismpdf_page_text_positioned(Handle, Native.ToIndex(index, nameof(index)), &text),
            "prismpdf_page_text_positioned");
        return Native.TakeString(text) ?? string.Empty;
    }

    /// <summary>
    /// Read one <c>/Info</c> entry by key, decoded per §7.9.2.2 — UTF-16BE and PDF 2.0 UTF-8 values
    /// both come back as .NET strings.
    /// </summary>
    /// <param name="key">The entry key, e.g. <c>Title</c> or <c>Producer</c>.</param>
    /// <returns>The value, or <see langword="null"/> when absent or non-textual.</returns>
    public string? Info(string key)
    {
        Throw.IfNull(key);

        var keyBytes = Native.AllocUtf8(key);
        try
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_document_info(Handle, keyBytes, &text),
                "prismpdf_document_info")
                ? Native.TakeString(text)
                : null;
        }
        finally
        {
            Native.FreeUtf8(keyBytes);
        }
    }

    /// <summary>How this document was opened: strictly, or by recovery, and what recovery saw.</summary>
    /// <returns>An owned report; dispose it when done.</returns>
    public OpenReport GetOpenReport()
    {
        nint report = 0;
        Native.Check(NativeMethods.prismpdf_document_open_report(Handle, &report),
            "prismpdf_document_open_report");
        return new OpenReport(report);
    }

    /// <summary>The structure namespaces the document declares (§14.7.4).</summary>
    /// <returns>An owned list; dispose it when done.</returns>
    public StringList StructureNamespaces() => ReadStringList(
        NativeMethods.prismpdf_document_structure_namespaces, "prismpdf_document_structure_namespaces");

    /// <summary>The <c>/VRI</c> keys in the document security store (§12.8.4.3).</summary>
    /// <returns>An owned list; dispose it when done.</returns>
    public StringList SignatureVriKeys() => ReadStringList(
        NativeMethods.prismpdf_document_signature_vri_keys, "prismpdf_document_signature_vri_keys");

    // -------------------------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------------------------

    /// <summary>The annotations on one page (§12.5).</summary>
    /// <param name="index">Zero-based page index.</param>
    /// <returns>An owned list — empty, not an error, when the page has no <c>/Annots</c>.</returns>
    public AnnotationList PageAnnotations(int index)
    {
        nint list = 0;
        Native.Check(
            NativeMethods.prismpdf_page_annotations(Handle, Native.ToIndex(index, nameof(index)), &list),
            "prismpdf_page_annotations");
        return new AnnotationList(list);
    }

    /// <summary>The images one page draws (§8.6, §8.9), recursing into form XObjects.</summary>
    /// <param name="index">Zero-based page index.</param>
    /// <returns>An owned list; dispose it when done.</returns>
    public ImageList PageImages(int index)
    {
        nint list = 0;
        Native.Check(
            NativeMethods.prismpdf_page_images(Handle, Native.ToIndex(index, nameof(index)), &list),
            "prismpdf_page_images");
        return new ImageList(list);
    }

    /// <summary>The terminal interactive form fields (§12.7).</summary>
    /// <returns>An owned list — empty when there is no AcroForm.</returns>
    public FormFieldList FormFields()
    {
        nint list = 0;
        Native.Check(NativeMethods.prismpdf_document_form_fields(Handle, &list),
            "prismpdf_document_form_fields");
        return new FormFieldList(list);
    }

    /// <summary>The outline (bookmark) tree's top level (§12.3.3).</summary>
    /// <returns>An owned list — empty without <c>/Outlines</c>. It owns the whole nested tree.</returns>
    public OutlineList Outline()
    {
        nint list = 0;
        Native.Check(NativeMethods.prismpdf_document_outline(Handle, &list), "prismpdf_document_outline");
        return new OutlineList(list);
    }

    /// <summary>The embedded files (§7.11), decoded through their filter chains.</summary>
    /// <returns>An owned list; dispose it when done.</returns>
    public AttachmentList Attachments()
    {
        nint list = 0;
        Native.Check(NativeMethods.prismpdf_document_attachments(Handle, &list),
            "prismpdf_document_attachments");
        return new AttachmentList(list);
    }

    /// <summary>Every font the pages reference, with its embedded program where present.</summary>
    /// <returns>An owned list; dispose it when done.</returns>
    public FontList Fonts()
    {
        nint list = 0;
        Native.Check(NativeMethods.prismpdf_document_fonts(Handle, &list), "prismpdf_document_fonts");
        return new FontList(list);
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_document_free(handle);

    private delegate PrismPdfStatus DateCall(nint doc, PrismPdfDate* outDate);

    private delegate PrismPdfStatus ListCall(nint doc, nint* outList);

    private PdfDate? ReadDate(DateCall call, string operation)
    {
        PrismPdfDate date = default;
        return Native.CheckOptional(call(Handle, &date), operation) ? PdfDate.From(date) : null;
    }

    private StringList ReadStringList(ListCall call, string operation)
    {
        nint list = 0;
        Native.Check(call(Handle, &list), operation);
        return new StringList(list);
    }
}
