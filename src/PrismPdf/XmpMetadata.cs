using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The XMP metadata a conformance pass writes into the document (§14.3.2).
/// </summary>
/// <remarks>
/// PDF/A and PDF/UA both require an XMP packet, and both require it to agree with the document's
/// <c>/Info</c> dictionary. Rather than have callers hand-write RDF, the engine takes these fields
/// and serialises the packet itself — including the identification schema that names the
/// conformance level. Every field is optional; what a standard requires it will refuse to build
/// without.
/// </remarks>
public sealed unsafe class XmpMetadata : PrismPdfHandle
{
    /// <summary>Create an empty metadata set.</summary>
    public XmpMetadata()
        : base(NativeMethods.prismpdf_xmp_metadata_new())
    {
    }

    /// <summary>The title (<c>dc:title</c>).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Title
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_title, value,
            "prismpdf_xmp_metadata_set_title");
    }

    /// <summary>The description (<c>dc:description</c>).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Subject
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_subject, value,
            "prismpdf_xmp_metadata_set_subject");
    }

    /// <summary>The keywords (<c>pdf:Keywords</c>).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Keywords
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_keywords, value,
            "prismpdf_xmp_metadata_set_keywords");
    }

    /// <summary>The authoring application (<c>xmp:CreatorTool</c>).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string CreatorTool
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_creator_tool, value,
            "prismpdf_xmp_metadata_set_creator_tool");
    }

    /// <summary>The producing library (<c>pdf:Producer</c>).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Producer
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_producer, value,
            "prismpdf_xmp_metadata_set_producer");
    }

    /// <summary>The creation date (<c>xmp:CreateDate</c>), as an ISO 8601 string.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string CreateDate
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_create_date, value,
            "prismpdf_xmp_metadata_set_create_date");
    }

    /// <summary>The modification date (<c>xmp:ModifyDate</c>), as an ISO 8601 string.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string ModifyDate
    {
        set => SetText(NativeMethods.prismpdf_xmp_metadata_set_modify_date, value,
            "prismpdf_xmp_metadata_set_modify_date");
    }

    /// <summary>
    /// Append an author (<c>dc:creator</c>). Call it repeatedly for several authors — this is the
    /// one XMP field that is a list rather than a single value.
    /// </summary>
    /// <param name="author">The author's name.</param>
    public void AddAuthor(string author)
        => SetText(NativeMethods.prismpdf_xmp_metadata_add_author, author,
            "prismpdf_xmp_metadata_add_author");

    /// <summary>Set <see cref="CreateDate"/> and <see cref="ModifyDate"/> from a .NET instant.</summary>
    /// <remarks>
    /// A convenience, not an ABI entry point: the ABI takes the ISO 8601 string XMP itself uses,
    /// and this formats one with an explicit offset, as XMP requires.
    /// </remarks>
    /// <param name="created">When the document was created.</param>
    /// <param name="modified">When it was last modified; defaults to <paramref name="created"/>.</param>
    public void SetDates(DateTimeOffset created, DateTimeOffset? modified = null)
    {
        CreateDate = created.ToString("yyyy-MM-ddTHH:mm:sszzz");
        ModifyDate = (modified ?? created).ToString("yyyy-MM-ddTHH:mm:sszzz");
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_xmp_metadata_free(handle);

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

    private delegate PrismPdfStatus TextSetter(nint meta, byte* value);
}
