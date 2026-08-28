using System.Collections;

namespace PrismPdf.Collections;

/// <summary>
/// Base class for the ABI's owned collection handles, projected as .NET read-only sequences.
/// </summary>
/// <remarks>
/// <para>
/// The ABI's collection convention is uniform: a producer writes an owned list handle, a
/// <c>*_list_len</c> reports the count, a <c>*_list_get</c> lends a borrowed item pointer valid
/// only while the list lives, and <c>*_list_free</c> invalidates every pointer lent from it. The
/// binding author's guide asks bindings to "prefer exposing each list as the language's native
/// read-only sequence of item wrappers", which is what this base does.
/// </para>
/// <para>
/// Items are re-fetched on every index, so indexing is a native call rather than a lookup into a
/// managed array. That is deliberate: it keeps the borrow relationship honest and costs nothing
/// for the sizes these lists reach.
/// </para>
/// </remarks>
/// <typeparam name="T">The borrowed item wrapper type.</typeparam>
public abstract class NativeList<T> : PrismPdfHandle, IReadOnlyList<T>
{
    private protected NativeList(nint handle)
        : base(handle)
    {
    }

    /// <summary>The number of items in the list.</summary>
    public abstract int Count { get; }

    /// <summary>Lend the item at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns>
    /// A lightweight wrapper that holds a reference to this list, so the collector cannot free the
    /// list while the item is reachable (semantic contract 4).
    /// </returns>
    /// <exception cref="PrismPdfException"><paramref name="index"/> is past the end.</exception>
    public abstract T this[int index] { get; }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        var count = Count;
        for (var i = 0; i < count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Base class for a <em>borrowed</em> item lent out of a <see cref="NativeList{T}"/>.
/// </summary>
/// <remarks>
/// A borrowed item is not owned and is never freed — it dies with its list. Holding a strong
/// reference to the owner is what keeps the garbage collector from freeing the list underneath a
/// reachable item, and checking the owner on every access is what turns "use after the list was
/// disposed" into an <see cref="ObjectDisposedException"/> instead of a segmentation fault.
/// </remarks>
public abstract class BorrowedItem
{
    private readonly PrismPdfHandle _owner;
    private readonly nint _item;

    private protected BorrowedItem(PrismPdfHandle owner, nint item)
    {
        _owner = owner;
        _item = item;
    }

    /// <summary>The owning list. Keeping it referenced is the whole point of this field.</summary>
    private protected PrismPdfHandle Owner => _owner;

    /// <summary>The borrowed pointer, checked against its owner still being alive.</summary>
    private protected nint Item
    {
        get
        {
            Throw.IfDisposed(_owner.IsInvalid, _owner);
            return _item;
        }
    }
}
