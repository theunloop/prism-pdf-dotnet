using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>
/// The top level of a document's outline (bookmark) tree (§12.3.3). Empty without <c>/Outlines</c>.
/// </summary>
/// <remarks>
/// The nested tree needs no per-level handle: this root list owns the whole tree, and a child is
/// lent straight out of its parent. Recursion to any depth allocates nothing on the native side,
/// and disposing this list invalidates every entry in the tree, nested ones included.
/// </remarks>
public sealed unsafe class OutlineList : NativeList<OutlineItem>
{
    internal OutlineList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_outline_list_len(Handle, &len), "prismpdf_outline_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override OutlineItem this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_outline_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_outline_list_get");
            return new OutlineItem(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_outline_list_free(handle);
}

/// <summary>One bookmark, borrowed from the <see cref="OutlineList"/> that owns its whole tree.</summary>
public sealed unsafe class OutlineItem : BorrowedItem
{
    internal OutlineItem(PrismPdfHandle owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary><c>/Title</c> (§7.9.2.2).</summary>
    public string Title
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_outline_item_title(Item, &text), "prismpdf_outline_item_title");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary>
    /// The zero-based destination page, or <see langword="null"/> when the destination does not
    /// resolve.
    /// </summary>
    public int? DestinationPage
    {
        get
        {
            nuint index = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_outline_item_dest_page(Item, &index),
                "prismpdf_outline_item_dest_page")
                ? Native.ToCount(index)
                : null;
        }
    }

    /// <summary>How many bookmarks are nested directly below this one.</summary>
    public int ChildCount
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_outline_item_child_count(Item, &len),
                "prismpdf_outline_item_child_count");
            return Native.ToCount(len);
        }
    }

    /// <summary>Lend the nested bookmark at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based index, below <see cref="ChildCount"/>.</param>
    /// <returns>A child borrowed from the same allocation as its parent.</returns>
    /// <exception cref="PrismPdfException"><paramref name="index"/> is past the end.</exception>
    public OutlineItem Child(int index)
    {
        nint child = 0;
        Native.Check(
            NativeMethods.prismpdf_outline_item_child(Item, Native.ToIndex(index, nameof(index)), &child),
            "prismpdf_outline_item_child");
        return new OutlineItem(Owner, child);
    }

    /// <summary>The nested bookmarks, in order.</summary>
    public IReadOnlyList<OutlineItem> Children()
    {
        var count = ChildCount;
        var items = new OutlineItem[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = Child(i);
        }

        return items;
    }

    /// <inheritdoc/>
    public override string ToString() => DestinationPage is { } page ? $"{Title} -> p{page}" : Title;
}
