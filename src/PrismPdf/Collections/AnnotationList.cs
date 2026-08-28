using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>The annotations on one page (§12.5). A page with no <c>/Annots</c> yields an empty list.</summary>
public sealed unsafe class AnnotationList : NativeList<Annotation>
{
    internal AnnotationList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_annotation_list_len(Handle, &len),
                "prismpdf_annotation_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override Annotation this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_annotation_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_annotation_list_get");
            return new Annotation(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_annotation_list_free(handle);
}

/// <summary>One annotation, borrowed from its <see cref="AnnotationList"/>.</summary>
public sealed unsafe class Annotation : BorrowedItem
{
    internal Annotation(AnnotationList owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary><c>/Subtype</c> — <c>Link</c>, <c>Text</c>, <c>Widget</c>, <c>Highlight</c>, ….</summary>
    public string Subtype
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_annotation_subtype(Item, &text),
                "prismpdf_annotation_subtype");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary><c>/Rect</c>, the annotation's rectangle on the page.</summary>
    public PdfRect Rect
    {
        get
        {
            var values = stackalloc double[4];
            Native.Check(NativeMethods.prismpdf_annotation_rect(Item, values),
                "prismpdf_annotation_rect");
            return new PdfRect(values[0], values[1], values[2], values[3]);
        }
    }

    /// <summary><c>/Contents</c>, or <see langword="null"/> when the annotation carries none.</summary>
    public string? Contents
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_annotation_contents(Item, &text),
                "prismpdf_annotation_contents")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <summary>
    /// The URI of a link with a URI action (§12.6.4.7), or <see langword="null"/> when this is not
    /// such a link.
    /// </summary>
    public string? Uri
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_annotation_uri(Item, &text),
                "prismpdf_annotation_uri")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <summary>
    /// The zero-based target page of an in-document link (§12.3.2), or <see langword="null"/> when
    /// the annotation is not one or the destination does not resolve.
    /// </summary>
    public int? DestinationPage
    {
        get
        {
            nuint index = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_annotation_dest_page(Item, &index),
                "prismpdf_annotation_dest_page")
                ? Native.ToCount(index)
                : null;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Uri is { } uri ? $"{Subtype} -> {uri}" : Subtype;
}
