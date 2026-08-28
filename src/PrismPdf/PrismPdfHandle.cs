namespace PrismPdf;

/// <summary>
/// Base class for every wrapper around an <em>owned</em> Prism PDF handle.
/// </summary>
/// <remarks>
/// <para>
/// Rule 4 of the binding author's guide maps <c>*_free</c> to "the language's disposal idiom …
/// never a public <c>free</c> method", and adds: "Deterministic disposal is the API; a finalizer,
/// if the language has one, is only a safety net." That is exactly what this class provides —
/// <see cref="IDisposable"/> for the contract, a finalizer so a forgotten <c>using</c> leaks
/// nothing permanently.
/// </para>
/// <para>
/// Semantic contract 3 — <em>consuming calls invalidate on success only</em> — lives here too.
/// A consuming call marks the handle dead through <see cref="MarkConsumed"/> when, and only when,
/// the native call returned <c>Ok</c>; a validation failure leaves the handle owned and usable.
/// Using a consumed or disposed wrapper afterwards raises <see cref="ObjectDisposedException"/>
/// from managed code, so the native library is never handed a dangling pointer.
/// </para>
/// <para>
/// The ABI makes no thread-safety promise for a handle (semantic contract 6). Confine an instance
/// to one thread, or guard it with your own lock.
/// </para>
/// </remarks>
public abstract class PrismPdfHandle : IDisposable
{
    private nint _handle;

    private protected PrismPdfHandle(nint handle)
    {
        if (handle == 0)
        {
            throw new PrismPdfHandleAllocationException(GetType().Name);
        }

        _handle = handle;
    }

    /// <summary>Releases the handle if the caller never did.</summary>
    ~PrismPdfHandle()
    {
        ReleaseIfLive();
    }

    /// <summary>
    /// Whether this wrapper no longer owns a handle — because it was disposed, or because a
    /// consuming call succeeded and the library took ownership.
    /// </summary>
    public bool IsInvalid => Volatile.Read(ref _handle) == IntPtr.Zero;

    /// <summary>The live handle, checked. Every native call in the SDK goes through this.</summary>
    internal nint Handle
    {
        get
        {
            var handle = _handle;
            Throw.IfDisposed(handle == 0, this);
            return handle;
        }
    }

    /// <summary>Release the handle now. Safe to call more than once.</summary>
    public void Dispose()
    {
        ReleaseIfLive();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Call the matching <c>*_free</c>. Implementations do nothing else: this runs on the
    /// finalizer thread when disposal was forgotten, where touching other managed objects is
    /// not safe.
    /// </summary>
    private protected abstract void Free(nint handle);

    /// <summary>
    /// Record that a successful consuming call took ownership of the handle. The wrapper stops
    /// tracking it <em>without</em> freeing it — the library owns those bytes now.
    /// </summary>
    private protected void MarkConsumed()
    {
        Interlocked.Exchange(ref _handle, IntPtr.Zero);
        GC.SuppressFinalize(this);
    }

    private void ReleaseIfLive()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            Free(handle);
        }
    }
}

/// <summary>
/// Thrown when a Prism PDF constructor that returns a bare pointer returned null — the ABI's way of
/// reporting "could not allocate, or the arguments do not describe a usable object".
/// </summary>
public sealed class PrismPdfHandleAllocationException : Exception
{
    internal PrismPdfHandleAllocationException(string typeName)
        : base($"Prism PDF returned a null handle while constructing {typeName}.")
    {
    }
}
