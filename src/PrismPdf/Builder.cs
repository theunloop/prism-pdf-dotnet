using System.Text;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A document under construction (§7.7): pages, metadata, annotations, attachments and logical
/// structure, serialised on demand.
/// </summary>
/// <remarks>
/// <para>
/// This is the authoring counterpart to <see cref="Document"/>. A document is something you opened;
/// a builder is something you are assembling, and it produces bytes rather than a handle —
/// <see cref="Build"/> serialises and leaves the builder intact, so pages can keep being added and
/// the document built again.
/// </para>
/// <para>
/// Two calls <em>consume</em> their argument: <see cref="AddPageSpec(PageSpec)"/> and
/// <see cref="AddStructureNode(StructNode)"/>. On success the argument belongs to the builder and
/// its wrapper is invalid; on a validation failure it stays owned by the caller. That is semantic
/// contract 3, and it is the reason those two take a handle rather than a value.
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
///
/// using var builder = new Builder { Title = "Greeting" };
/// builder.AddPage(content, new Dictionary&lt;string, StdFont&gt; { ["F1"] = StdFont.Helvetica });
/// var pdf = builder.Build();
/// </code>
/// </example>
public sealed unsafe partial class Builder : PrismPdfHandle
{
    /// <summary>Create an empty builder — US Letter pages, no metadata, no pages.</summary>
    public Builder()
        : base(NativeMethods.prismpdf_builder_new())
    {
    }

    private Builder(nint handle)
        : base(handle)
    {
    }

    /// <summary>
    /// The default page box (<c>/MediaBox</c>, §7.7.3.3) for pages added after this is set.
    /// Defaults to US Letter.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public PdfRect MediaBox
    {
        set
        {
            var box = stackalloc double[4] { value.Llx, value.Lly, value.Urx, value.Ury };
            Native.Check(NativeMethods.prismpdf_builder_set_media_box(Handle, box),
                "prismpdf_builder_set_media_box");
        }
    }

    /// <summary>The document title (<c>/Title</c>, §14.3.3) — PDF/UA requires one.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Title
    {
        set => SetText(NativeMethods.prismpdf_builder_set_title, value, "prismpdf_builder_set_title");
    }

    /// <summary>The author (<c>/Author</c>, §14.3.3).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Author
    {
        set => SetText(NativeMethods.prismpdf_builder_set_author, value, "prismpdf_builder_set_author");
    }

    /// <summary>The subject (<c>/Subject</c>, §14.3.3).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Subject
    {
        set => SetText(NativeMethods.prismpdf_builder_set_subject, value, "prismpdf_builder_set_subject");
    }

    /// <summary>The keywords (<c>/Keywords</c>, §14.3.3).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Keywords
    {
        set => SetText(NativeMethods.prismpdf_builder_set_keywords, value, "prismpdf_builder_set_keywords");
    }

    /// <summary>The creating application (<c>/Creator</c>, §14.3.3).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Creator
    {
        set => SetText(NativeMethods.prismpdf_builder_set_creator, value, "prismpdf_builder_set_creator");
    }

    /// <summary>
    /// The document's natural language (<c>/Lang</c>, §14.9.2), e.g. <c>en-GB</c> — required by
    /// PDF/UA.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Lang
    {
        set => SetText(NativeMethods.prismpdf_builder_set_lang, value, "prismpdf_builder_set_lang");
    }

    /// <summary>
    /// The PDF 2.0 namespace URI on the implicit <c>Document</c> structure root (§14.7.4).
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string StructureNamespace
    {
        set => SetText(NativeMethods.prismpdf_builder_set_structure_namespace, value,
            "prismpdf_builder_set_structure_namespace");
    }

    /// <summary>
    /// <c>/ViewerPreferences /DisplayDocTitle</c> (§12.2) — PDF/UA requires it on, so viewers show
    /// the document title rather than the file name.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public bool DisplayDocTitle
    {
        set => Native.Check(
            NativeMethods.prismpdf_builder_set_display_doc_title(Handle, Native.FromBool(value)),
            "prismpdf_builder_set_display_doc_title");
    }

    // -----------------------------------------------------------------------------------------
    // Serialising
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Serialise the document, stamping the <em>minimum</em> header version its content requires
    /// (§7.5.2) unless one was pinned with <see cref="SetVersion(int, int)"/>.
    /// </summary>
    /// <remarks>The builder is not consumed: keep adding pages and build again.</remarks>
    /// <returns>The serialized PDF.</returns>
    public byte[] Build()
    {
        byte* data = null;
        nuint len = 0;
        Native.Check(NativeMethods.prismpdf_builder_build(Handle, &data, &len),
            "prismpdf_builder_build");
        return Native.TakeBytes(data, len);
    }

    /// <summary>
    /// Serialise declaring exactly the target version (§7.5.2), guaranteeing the output contains
    /// only constructs valid at that version.
    /// </summary>
    /// <remarks>
    /// Constructs above the target are <em>refused</em> rather than silently downgraded, so a
    /// failure here names a real incompatibility.
    /// </remarks>
    /// <param name="major">Target major version.</param>
    /// <param name="minor">Target minor version.</param>
    /// <returns>The serialized PDF.</returns>
    public byte[] BuildFor(int major, int minor)
    {
        byte* data = null;
        nuint len = 0;
        Native.Check(
            NativeMethods.prismpdf_builder_build_for(
                Handle, ToVersionPart(major, nameof(major)), ToVersionPart(minor, nameof(minor)),
                &data, &len),
            "prismpdf_builder_build_for");
        return Native.TakeBytes(data, len);
    }

    /// <summary>
    /// Pin the header version (§7.5.2). This is a <em>floor</em>: <see cref="Build"/> never stamps
    /// below what the content requires, but an explicit value above the minimum is honoured.
    /// </summary>
    /// <param name="major">Major version.</param>
    /// <param name="minor">Minor version.</param>
    public void SetVersion(int major, int minor)
        => Native.Check(
            NativeMethods.prismpdf_builder_set_version(
                Handle, ToVersionPart(major, nameof(major)), ToVersionPart(minor, nameof(minor))),
            "prismpdf_builder_set_version");

    // -----------------------------------------------------------------------------------------
    // Pages
    // -----------------------------------------------------------------------------------------

    /// <summary>Append a page carrying this content and no font resources.</summary>
    /// <param name="content">The page's operators. Copied; not retained.</param>
    public void AddPage(Content content) => AddPage(content, null);

    /// <summary>
    /// Append a page carrying this content, exposing the given Standard-14 fonts in its
    /// <c>/Resources /Font</c> — the names <see cref="Content.SetFont(string, double)"/> selects.
    /// </summary>
    /// <param name="content">The page's operators. Copied; not retained.</param>
    /// <param name="fonts">Resource name to Standard-14 font, e.g. <c>["F1"] = StdFont.Helvetica</c>.</param>
    public void AddPage(Content content, IReadOnlyDictionary<string, StdFont>? fonts)
    {
        Throw.IfNull(content);

        var count = fonts?.Count ?? 0;
        var names = new string[count];
        var values = new StdFont[count];
        if (fonts is not null)
        {
            var i = 0;
            foreach (var entry in fonts)
            {
                names[i] = entry.Key ?? throw new ArgumentException("A font name is null.", nameof(fonts));
                values[i] = entry.Value;
                i++;
            }
        }

        using var fontNames = new NativeUtf8Array(names, nameof(fonts));
        content.BorrowBytes(out var data, out var len);
        fixed (StdFont* stdFonts = values)
        {
            Native.Check(
                NativeMethods.prismpdf_builder_add_page(
                    Handle, data, len, fontNames.Pointers, stdFonts, fontNames.Count),
                "prismpdf_builder_add_page");
        }

        // The byte view above is lent by `content` and dies with it; keep it reachable until the
        // call that read it has returned.
        GC.KeepAlive(content);
    }

    /// <summary>
    /// Append an assembled page specification (§7.7.3.3). <b>Consuming:</b> on success
    /// <paramref name="page"/> belongs to the builder and its wrapper is invalid.
    /// </summary>
    /// <param name="page">The page to transfer.</param>
    public void AddPageSpec(PageSpec page)
    {
        Throw.IfNull(page);

        var status = NativeMethods.prismpdf_builder_add_page_spec(Handle, page.Handle);
        if (status == PrismPdfStatus.Ok)
        {
            page.MarkTransferred();
        }

        Native.Check(status, "prismpdf_builder_add_page_spec");
    }

    // -----------------------------------------------------------------------------------------
    // Metadata
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Set an arbitrary <c>/Info</c> entry (§14.3.3) by key, replacing any previous value for it.
    /// </summary>
    /// <param name="key">The entry key, without its leading slash.</param>
    /// <param name="value">The entry value.</param>
    public void SetInfo(string key, string value)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_builder_set_info(
                Handle, strings.Add(key, nameof(key)), strings.Add(value, nameof(value))),
            "prismpdf_builder_set_info");
    }

    /// <summary>
    /// Drop every <c>/Info</c> entry set so far — PDF/A-4 and PDF 2.0 prefer XMP as the sole
    /// metadata source (§14.3).
    /// </summary>
    public void ClearInfo()
        => Native.Check(NativeMethods.prismpdf_builder_clear_info(Handle), "prismpdf_builder_clear_info");

    /// <summary>Attach an XMP metadata packet (§14.3.2) as the document's <c>/Metadata</c> stream.</summary>
    /// <param name="xmp">The packet's bytes.</param>
    public void SetMetadataXmp(ReadOnlySpan<byte> xmp)
    {
        fixed (byte* bytes = xmp)
        {
            Native.Check(
                NativeMethods.prismpdf_builder_set_metadata_xmp(Handle, bytes, (nuint)xmp.Length),
                "prismpdf_builder_set_metadata_xmp");
        }
    }

    /// <inheritdoc cref="SetMetadataXmp(ReadOnlySpan{byte})"/>
    /// <param name="xmp">The packet, encoded as UTF-8 before it crosses.</param>
    public void SetMetadataXmp(string xmp)
    {
        Throw.IfNull(xmp);
        SetMetadataXmp(Encoding.UTF8.GetBytes(xmp));
    }

    /// <summary>
    /// Set the permanent file identifier (<c>/ID</c> element 1, §14.4) instead of letting the
    /// writer derive one from the content.
    /// </summary>
    /// <param name="id">The identifier's bytes.</param>
    public void SetFileId(ReadOnlySpan<byte> id)
    {
        fixed (byte* bytes = id)
        {
            Native.Check(NativeMethods.prismpdf_builder_set_file_id(Handle, bytes, (nuint)id.Length),
                "prismpdf_builder_set_file_id");
        }
    }

    /// <summary>
    /// Write text strings as UTF-8 with a BOM (§7.9.2.2) rather than UTF-16BE — a PDF 2.0 form
    /// that <see cref="BuildFor(int, int)"/> downgrades automatically below 2.0.
    /// </summary>
    public void SetUtf8TextStrings()
        => Native.Check(NativeMethods.prismpdf_builder_set_utf8_text_strings(Handle),
            "prismpdf_builder_set_utf8_text_strings");

    /// <summary>
    /// Set the document's OutputIntent (§14.11.5) directly, without running a conformance pass.
    /// </summary>
    /// <param name="iccProfile">The ICC profile's bytes.</param>
    /// <param name="componentCount">The profile's colour-component count: 1 Gray, 3 RGB, 4 CMYK.</param>
    /// <param name="identifier">The output-condition identifier, e.g. <c>sRGB</c>.</param>
    public void SetOutputIntent(ReadOnlySpan<byte> iccProfile, int componentCount, string identifier)
    {
        Throw.IfNegative(componentCount);

        using var strings = new Utf8Scope();
        var id = strings.Add(identifier, nameof(identifier));
        fixed (byte* icc = iccProfile)
        {
            Native.Check(
                NativeMethods.prismpdf_builder_set_output_intent(
                    Handle, icc, (nuint)iccProfile.Length, (uint)componentCount, id),
                "prismpdf_builder_set_output_intent");
        }
    }

    // -----------------------------------------------------------------------------------------
    // Outline, attachments, annotations
    // -----------------------------------------------------------------------------------------

    /// <summary>Add a top-level bookmark (§12.3.3) jumping to a page.</summary>
    /// <param name="title">The bookmark's title.</param>
    /// <param name="pageIndex">Zero-based target page.</param>
    public void AddOutline(string title, int pageIndex)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_builder_add_outline(
                Handle, strings.Add(title, nameof(title)), Native.ToIndex(pageIndex, nameof(pageIndex))),
            "prismpdf_builder_add_outline");
    }

    /// <summary>
    /// Embed a file (§7.11) and list it in the <c>/EmbeddedFiles</c> name tree (§7.7.4).
    /// </summary>
    /// <param name="name">The file name recorded in the file specification.</param>
    /// <param name="mime">
    /// The MIME type; pass <c>application/octet-stream</c> when nothing better applies.
    /// </param>
    /// <param name="relationship">
    /// The <c>/AFRelationship</c> value (§14.13); pass <c>Unspecified</c> when nothing better
    /// applies.
    /// </param>
    /// <param name="description">An optional human-readable description.</param>
    /// <param name="data">The file's bytes.</param>
    public void AttachFile(
        string name,
        string mime,
        string relationship,
        string? description,
        ReadOnlySpan<byte> data)
    {
        using var strings = new Utf8Scope();
        var namePtr = strings.Add(name, nameof(name));
        var mimePtr = strings.Add(mime, nameof(mime));
        var relationshipPtr = strings.Add(relationship, nameof(relationship));
        var descriptionPtr = strings.AddOptional(description);

        fixed (byte* bytes = data)
        {
            Native.Check(
                NativeMethods.prismpdf_builder_attach_file(
                    Handle, namePtr, mimePtr, relationshipPtr, descriptionPtr, bytes, (nuint)data.Length),
                "prismpdf_builder_attach_file");
        }
    }

    /// <summary>
    /// Add a hyperlink annotation (§12.5.6.5) pointing at an external URI.
    /// </summary>
    /// <remarks>
    /// The ABI exposes one entry point per link target rather than one taking a tagged union,
    /// because C cannot carry a Rust enum's payload. This SDK keeps them separate for the same
    /// reason every other binding does: so a link target reads the same in every language.
    /// </remarks>
    /// <param name="pageIndex">Zero-based page the link sits on.</param>
    /// <param name="rect">The link's rectangle, in default user space.</param>
    /// <param name="uri">The target URI.</param>
    /// <param name="contents">
    /// The alternate description PDF/UA wants on links (§7.18.5). Optional, but supply it.
    /// </param>
    public void AddLinkUri(int pageIndex, PdfRect rect, string uri, string? contents = null)
    {
        using var strings = new Utf8Scope();
        var uriPtr = strings.Add(uri, nameof(uri));
        var contentsPtr = strings.AddOptional(contents);
        var box = stackalloc double[4] { rect.Llx, rect.Lly, rect.Urx, rect.Ury };

        Native.Check(
            NativeMethods.prismpdf_builder_add_link_uri(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), box, uriPtr, contentsPtr),
            "prismpdf_builder_add_link_uri");
    }

    /// <summary>Add a hyperlink annotation jumping to another page in the same document (§12.3.2).</summary>
    /// <param name="pageIndex">Zero-based page the link sits on.</param>
    /// <param name="rect">The link's rectangle, in default user space.</param>
    /// <param name="targetPage">Zero-based page the link jumps to.</param>
    /// <param name="contents">The alternate description PDF/UA wants on links (§7.18.5).</param>
    public void AddLinkPage(int pageIndex, PdfRect rect, int targetPage, string? contents = null)
    {
        using var strings = new Utf8Scope();
        var contentsPtr = strings.AddOptional(contents);
        var box = stackalloc double[4] { rect.Llx, rect.Lly, rect.Urx, rect.Ury };

        Native.Check(
            NativeMethods.prismpdf_builder_add_link_page(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), box,
                Native.ToIndex(targetPage, nameof(targetPage)), contentsPtr),
            "prismpdf_builder_add_link_page");
    }

    /// <summary>
    /// Add a hyperlink annotation jumping to a <em>structure element</em> by its <c>/ID</c> (a
    /// structure destination, §12.3.2.2 — PDF 2.0), which is what PDF/UA-2 wants instead of a page
    /// destination.
    /// </summary>
    /// <param name="pageIndex">Zero-based page the link sits on.</param>
    /// <param name="rect">The link's rectangle, in default user space.</param>
    /// <param name="elementId">The <see cref="StructNode.Id"/> of the target element.</param>
    /// <param name="contents">The alternate description PDF/UA wants on links (§7.18.5).</param>
    public void AddLinkElement(int pageIndex, PdfRect rect, string elementId, string? contents = null)
    {
        using var strings = new Utf8Scope();
        var elementPtr = strings.Add(elementId, nameof(elementId));
        var contentsPtr = strings.AddOptional(contents);
        var box = stackalloc double[4] { rect.Llx, rect.Lly, rect.Urx, rect.Ury };

        Native.Check(
            NativeMethods.prismpdf_builder_add_link_element(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), box, elementPtr, contentsPtr),
            "prismpdf_builder_add_link_element");
    }

    /// <summary>Add a hyperlink annotation jumping to a document part (§14.12 — PDF 2.0).</summary>
    /// <param name="pageIndex">Zero-based page the link sits on.</param>
    /// <param name="rect">The link's rectangle, in default user space.</param>
    /// <param name="partIndex">Zero-based document part the link jumps to.</param>
    /// <param name="contents">The alternate description PDF/UA wants on links (§7.18.5).</param>
    public void AddLinkDocumentPart(int pageIndex, PdfRect rect, int partIndex, string? contents = null)
    {
        using var strings = new Utf8Scope();
        var contentsPtr = strings.AddOptional(contents);
        var box = stackalloc double[4] { rect.Llx, rect.Lly, rect.Urx, rect.Ury };

        Native.Check(
            NativeMethods.prismpdf_builder_add_link_document_part(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), box,
                Native.ToIndex(partIndex, nameof(partIndex)), contentsPtr),
            "prismpdf_builder_add_link_document_part");
    }

    /// <summary>
    /// Add a text-note annotation (§12.5.6.4) carrying <paramref name="contents"/> as its body. A
    /// normal appearance stream is generated, as PDF/A requires for non-link annotations.
    /// </summary>
    /// <param name="pageIndex">Zero-based page the note sits on.</param>
    /// <param name="rect">The note's rectangle, in default user space.</param>
    /// <param name="contents">The note's text.</param>
    public void AddNote(int pageIndex, PdfRect rect, string contents)
    {
        using var strings = new Utf8Scope();
        var contentsPtr = strings.Add(contents, nameof(contents));
        var box = stackalloc double[4] { rect.Llx, rect.Lly, rect.Urx, rect.Ury };

        Native.Check(
            NativeMethods.prismpdf_builder_add_note(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), box, contentsPtr),
            "prismpdf_builder_add_note");
    }

    /// <summary>
    /// Add a checkbox form field (<c>/FT /Btn</c>, §12.7.4.2.3) as a widget. Its on-state is named
    /// <c>On</c>, and its appearance is vector-drawn, so it needs no font.
    /// </summary>
    /// <param name="pageIndex">Zero-based page the widget sits on.</param>
    /// <param name="rect">The widget's rectangle, in default user space.</param>
    /// <param name="name">The field name (<c>/T</c>).</param>
    /// <param name="isChecked">Whether the box starts checked.</param>
    /// <param name="tooltip">
    /// The field's <c>/TU</c> (§12.7.3.1). Optional, but PDF/UA wants one — assistive technology
    /// reads it in place of the field name.
    /// </param>
    public void AddCheckbox(
        int pageIndex,
        PdfRect rect,
        string name,
        bool isChecked,
        string? tooltip = null)
    {
        using var strings = new Utf8Scope();
        var namePtr = strings.Add(name, nameof(name));
        var tooltipPtr = strings.AddOptional(tooltip);
        var box = stackalloc double[4] { rect.Llx, rect.Lly, rect.Urx, rect.Ury };

        Native.Check(
            NativeMethods.prismpdf_builder_add_checkbox(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), box, namePtr,
                Native.FromBool(isChecked), tooltipPtr),
            "prismpdf_builder_add_checkbox");
    }

    // -----------------------------------------------------------------------------------------
    // Logical structure
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Transfer one top-level structure element to the builder (§14.7). <b>Consuming:</b> on
    /// success <paramref name="node"/> belongs to the builder and its wrapper is invalid.
    /// </summary>
    /// <param name="node">The structure subtree to transfer.</param>
    public void AddStructureNode(StructNode node)
    {
        Throw.IfNull(node);

        var status = NativeMethods.prismpdf_builder_add_structure_node(Handle, node.Handle);
        if (status == PrismPdfStatus.Ok)
        {
            node.MarkTransferred();
        }

        Native.Check(status, "prismpdf_builder_add_structure_node");
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_builder_free(handle);

    /// <summary>
    /// Wrap a builder the library produced rather than one this SDK constructed — the flow's
    /// <see cref="Flow.IntoBuilder"/> is the only source.
    /// </summary>
    internal static Builder FromHandle(nint handle) => new(handle);

    private static byte ToVersionPart(int value, string parameterName)
    {
        Throw.IfNegative(value, parameterName);
        Throw.IfGreaterThan(value, byte.MaxValue, parameterName);
        return (byte)value;
    }

    private void SetText(TextSetter setter, string value, string operation)
    {
        Throw.IfNull(value);

        var text = Native.AllocUtf8(value);
        try
        {
            Native.Check(setter(Handle, text), operation);
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    private delegate PrismPdfStatus TextSetter(nint builder, byte* value);
}
