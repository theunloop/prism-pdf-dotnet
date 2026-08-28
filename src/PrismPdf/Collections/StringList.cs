using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>
/// An owned list of strings.
/// </summary>
/// <remarks>
/// Unlike the byte payloads on other list items, entries here are <em>copied</em> rather than
/// borrowed: a C string needs a NUL terminator that the underlying Rust <c>String</c> does not
/// carry, so the ABI allocates per entry and this wrapper releases each one immediately.
/// </remarks>
public sealed unsafe class StringList : NativeList<string>
{
    internal StringList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_string_list_len(Handle, &len), "prismpdf_string_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override string this[int index]
    {
        get
        {
            byte* text = null;
            Native.Check(
                NativeMethods.prismpdf_string_list_get(Handle, Native.ToIndex(index, nameof(index)), &text),
                "prismpdf_string_list_get");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary>Copy every entry into a managed array and release the native list.</summary>
    /// <remarks>
    /// Convenient when the list is small and you would rather not manage its lifetime: the strings
    /// are managed copies, so they outlive this handle.
    /// </remarks>
    /// <returns>The entries, in order.</returns>
    public string[] ToArrayAndDispose()
    {
        try
        {
            var items = new string[Count];
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = this[i];
            }

            return items;
        }
        finally
        {
            Dispose();
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_string_list_free(handle);
}
