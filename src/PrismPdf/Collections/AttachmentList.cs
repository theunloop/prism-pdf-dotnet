using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>
/// A document's embedded files (§7.11), read out of the <c>/EmbeddedFiles</c> name tree (§7.7.4)
/// and decoded through each file's filter chain.
/// </summary>
public sealed unsafe class AttachmentList : NativeList<Attachment>
{
    internal AttachmentList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_attachment_list_len(Handle, &len),
                "prismpdf_attachment_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override Attachment this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_attachment_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_attachment_list_get");
            return new Attachment(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_attachment_list_free(handle);
}

/// <summary>One embedded file, borrowed from its <see cref="AttachmentList"/>.</summary>
public sealed unsafe class Attachment : BorrowedItem
{
    internal Attachment(AttachmentList owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary>The file name — <c>/UF</c> preferred, else <c>/F</c>, else the name-tree key.</summary>
    public string Name
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_attachment_name(Item, &text), "prismpdf_attachment_name");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary>
    /// The decoded file bytes, copied out of the list's own allocation.
    /// </summary>
    /// <remarks>
    /// The ABI lends a borrowed view here rather than copying, so this property is the copy. An
    /// empty payload comes back as an empty array, never as a dangling pointer.
    /// </remarks>
    public byte[] Data
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_attachment_data(Item, &data, &len), "prismpdf_attachment_data");
            return Native.CopyBorrowedBytes(data, len);
        }
    }

    /// <summary><c>/EmbeddedFile /Subtype</c>, or <see langword="null"/> when the file declares none.</summary>
    public string? MimeType
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_attachment_mime(Item, &text),
                "prismpdf_attachment_mime")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <summary><c>/AFRelationship</c> (§14.13), or <see langword="null"/> when unset.</summary>
    public string? Relationship
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_attachment_relationship(Item, &text),
                "prismpdf_attachment_relationship")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <summary><c>/Desc</c>, or <see langword="null"/> when unset.</summary>
    public string? Description
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_attachment_description(Item, &text),
                "prismpdf_attachment_description")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => MimeType is { } mime ? $"{Name} ({mime})" : Name;
}
