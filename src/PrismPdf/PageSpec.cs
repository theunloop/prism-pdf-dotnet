using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A page under construction: its content stream plus the resources that stream names (§7.7.3.3).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Builder.AddPage(Content)"/> covers the common case — a content stream and some
/// Standard-14 fonts. A page specification is what to reach for when the page needs more than
/// that: its own media box, an embedded font, or an image resource.
/// </para>
/// <para>
/// The constructor <em>copies</em> the content bytes, so the <see cref="Content"/> it was built
/// from may be reused or disposed immediately afterwards. The specification itself is
/// <em>consumed</em> by <see cref="Builder.AddPageSpec(PageSpec)"/> on success, after which this
/// wrapper is invalid — that is semantic contract 3, and using it afterwards raises
/// <see cref="ObjectDisposedException"/> rather than handing the library a dangling pointer.
/// </para>
/// </remarks>
public sealed unsafe class PageSpec : PrismPdfHandle
{
    /// <summary>Create a page specification from assembled content (§7.8.2).</summary>
    /// <param name="content">The page's operators. Copied; not retained.</param>
    public PageSpec(Content content)
        : base(NewHandle(content))
    {
    }

    /// <summary>
    /// This page's media box (<c>/MediaBox</c>, §14.11.2), overriding the builder's default.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public PdfRect MediaBox
    {
        set => Native.Check(
            NativeMethods.prismpdf_page_spec_set_media_box(Handle, value.Llx, value.Lly, value.Urx, value.Ury),
            "prismpdf_page_spec_set_media_box");
    }

    /// <summary>Add a named Standard-14 font resource (§9.6.2.2).</summary>
    /// <param name="name">The resource name a <c>Tf</c> operator will select, e.g. <c>F1</c>.</param>
    /// <param name="font">Which of the fourteen.</param>
    public void AddStandardFont(string name, StdFont font)
    {
        Throw.IfNull(name);

        var text = Native.AllocUtf8(name);
        try
        {
            Native.Check(NativeMethods.prismpdf_page_spec_add_standard_font(Handle, text, font),
                "prismpdf_page_spec_add_standard_font");
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    /// <summary>
    /// Reference a CID font already registered on the builder under this name (§9.7).
    /// </summary>
    /// <param name="name">The resource name a <c>Tf</c> operator will select.</param>
    public void AddEmbeddedFont(string name)
    {
        Throw.IfNull(name);

        var text = Native.AllocUtf8(name);
        try
        {
            Native.Check(NativeMethods.prismpdf_page_spec_add_embedded_font(Handle, text),
                "prismpdf_page_spec_add_embedded_font");
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    /// <summary>
    /// Add a named image XObject resource (§8.9), for a <c>Do</c> operator to draw.
    /// </summary>
    /// <param name="name">The resource name, e.g. <c>Im1</c>.</param>
    /// <param name="image">The samples. Copied; the source stays owned by the caller.</param>
    public void AddImage(string name, ImageSource image)
    {
        Throw.IfNull(name);
        Throw.IfNull(image);

        var text = Native.AllocUtf8(name);
        try
        {
            Native.Check(NativeMethods.prismpdf_page_spec_add_image(Handle, text, image.Handle),
                "prismpdf_page_spec_add_image");
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_page_spec_free(handle);

    /// <summary>
    /// Record that the builder took ownership. Called only after a consuming call returned
    /// <c>Ok</c> — semantic contract 3.
    /// </summary>
    internal void MarkTransferred() => MarkConsumed();

    private static nint NewHandle(Content content)
    {
        Throw.IfNull(content);
        return NativeMethods.prismpdf_page_spec_new(content.Handle);
    }
}
