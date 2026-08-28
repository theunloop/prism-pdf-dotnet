using System.Runtime.InteropServices;

namespace PrismPdf.Interop;

/// <summary>
/// The unmanaged allocator the marshalling helpers use, in the shape netstandard2.0 lacks.
/// </summary>
/// <remarks>
/// <para>
/// .NET 6 added <c>System.Runtime.InteropServices.NativeMemory</c>; netstandard2.0 has only
/// <see cref="Marshal"/>'s allocators. This is a straight forward to <c>Marshal.AllocHGlobal</c>
/// with the same contract: the returned block is uninitialised, and <see cref="Free"/> is the only
/// legal way to release it.
/// </para>
/// <para>
/// It allocates <em>our</em> memory, never the library's. Buffers that came from Prism PDF are
/// released with <c>prismpdf_string_free</c> / <c>prismpdf_bytes_free</c> and must never reach here.
/// </para>
/// </remarks>
internal static unsafe class NativeMemory
{
    /// <summary>Allocate <paramref name="byteCount"/> uninitialised bytes.</summary>
    internal static void* Alloc(nuint byteCount)
    {
        // AllocHGlobal(0) is legal and returns a non-null, non-dereferenceable block; keeping it
        // means callers never have to special-case an empty payload.
        return (void*)Marshal.AllocHGlobal(new IntPtr(checked((long)byteCount)));
    }

    /// <summary>Allocate <paramref name="elementCount"/> × <paramref name="elementSize"/> uninitialised bytes.</summary>
    internal static void* Alloc(nuint elementCount, nuint elementSize)
    {
        return Alloc(checked(elementCount * elementSize));
    }

    /// <summary>Release a block from <see cref="Alloc(nuint)"/>. Null is a no-op, as in .NET 6+.</summary>
    internal static void Free(void* ptr)
    {
        if (ptr is not null)
        {
            Marshal.FreeHGlobal((IntPtr)ptr);
        }
    }
}
