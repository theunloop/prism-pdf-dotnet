using System.Runtime.InteropServices;
using System.Text;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The conversions the ownership contract demands, in one place. Every rule in
/// <c>docs/site/ffi-ownership.md</c> is implemented exactly once here, so no wrapper method can
/// get it subtly wrong.
/// </summary>
internal static unsafe class Native
{
    /// <summary>Throw for any status other than <see cref="PrismPdfStatus.Ok"/>.</summary>
    internal static void Check(PrismPdfStatus status, string operation)
    {
        if (status != PrismPdfStatus.Ok)
        {
            throw PrismPdfException.Create(status, operation);
        }
    }

    /// <summary>
    /// Throw for any status other than <c>Ok</c> or <c>NotFound</c>, and report which it was.
    /// The shape of an <em>optional</em> getter: <c>NotFound</c> means the field is absent.
    /// </summary>
    internal static bool CheckOptional(PrismPdfStatus status, string operation)
    {
        if (status == PrismPdfStatus.NotFound)
        {
            return false;
        }

        Check(status, operation);
        return true;
    }

    /// <summary>
    /// Copy an owned <c>char *</c> into a managed string and release it with
    /// <c>prismpdf_string_free</c> — "copy, then free, immediately" (semantic contract 5).
    /// </summary>
    internal static string? TakeString(byte* text)
    {
        if (text is null)
        {
            return null;
        }

        try
        {
            return Utf8ToString(text);
        }
        finally
        {
            NativeMethods.prismpdf_string_free(text);
        }
    }

    /// <summary>
    /// Decode a NUL-terminated UTF-8 <c>char *</c> into a managed string, without taking
    /// ownership of it.
    /// </summary>
    /// <remarks>
    /// <c>Marshal.PtrToStringUTF8</c> would say this in one line, but it is netstandard2.1+.
    /// </remarks>
    internal static string? Utf8ToString(byte* text)
    {
        if (text is null)
        {
            return null;
        }

        var length = 0;
        while (text[length] != 0)
        {
            length++;
        }

        return Encoding.UTF8.GetString(text, length);
    }

    /// <summary>
    /// Copy an owned <c>(uint8_t *, uintptr_t)</c> buffer and release it with
    /// <c>prismpdf_bytes_free</c>, passing back the original length as the ABI requires.
    /// </summary>
    internal static byte[] TakeBytes(byte* data, nuint len)
    {
        if (data is null)
        {
            return [];
        }

        try
        {
            var buffer = new byte[checked((int)len)];
            new ReadOnlySpan<byte>(data, buffer.Length).CopyTo(buffer);
            return buffer;
        }
        finally
        {
            NativeMethods.prismpdf_bytes_free(data, len);
        }
    }

    /// <summary>
    /// Copy a <em>borrowed</em> byte view out of a list item. Borrowed views are never freed —
    /// they die with their owning list — so this copies and returns, nothing more. An empty
    /// payload lends a null pointer with length 0 rather than a dangling one.
    /// </summary>
    internal static byte[] CopyBorrowedBytes(byte* data, nuint len)
    {
        if (data is null || len == 0)
        {
            return [];
        }

        var buffer = new byte[checked((int)len)];
        new ReadOnlySpan<byte>(data, buffer.Length).CopyTo(buffer);
        return buffer;
    }

    /// <summary>
    /// Encode a managed string as NUL-terminated UTF-8 for a <c>const char *</c> parameter. The
    /// caller owns the result and must release it with <see cref="FreeUtf8"/>.
    /// </summary>
    internal static byte* AllocUtf8(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var buffer = (byte*)NativeMemory.Alloc((nuint)byteCount + 1);
        fixed (char* chars = value)
        {
            Encoding.UTF8.GetBytes(chars, value.Length, buffer, byteCount);
        }

        buffer[byteCount] = 0;
        return buffer;
    }

    /// <summary>Release a buffer from <see cref="AllocUtf8"/>. Not library memory — ours.</summary>
    internal static void FreeUtf8(byte* buffer) => NativeMemory.Free(buffer);

    /// <summary>Project a one-byte C <c>bool</c> out-param.</summary>
    internal static bool ToBool(byte value) => value != 0;

    /// <summary>Project a managed bool into the one-byte C <c>bool</c> the ABI expects.</summary>
    internal static byte FromBool(bool value) => value ? (byte)1 : (byte)0;

    /// <summary>
    /// Narrow a <c>uintptr_t</c> count to <see cref="int"/>, the type .NET collections index with.
    /// A count that does not fit is a corrupt library state, not a user error.
    /// </summary>
    internal static int ToCount(nuint value)
    {
        if (value > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Prism PDF reported {value} items, which exceeds the addressable range of a .NET collection.");
        }

        return (int)value;
    }

    /// <summary>Widen a non-negative index to <c>uintptr_t</c>, rejecting negatives up front.</summary>
    internal static nuint ToIndex(int value, string parameterName)
    {
        Throw.IfNegative(value, parameterName);
        return (nuint)value;
    }
}

/// <summary>
/// A parallel <c>(const uint8_t *const *, const uintptr_t *)</c> pair, as the ABI takes for a list
/// of DER blobs (certificates, trust anchors). Owns one native copy of each blob for the duration
/// of the call.
/// </summary>
internal sealed unsafe class NativeByteArrays : IDisposable
{
    private readonly int _count;

    internal NativeByteArrays(IReadOnlyList<byte[]> items, string parameterName)
    {
        Throw.IfNull(items, parameterName);

        _count = items.Count;
        Pointers = (byte**)NativeMemory.Alloc((nuint)Math.Max(_count, 1), (nuint)sizeof(byte*));
        Lengths = (nuint*)NativeMemory.Alloc((nuint)Math.Max(_count, 1), (nuint)sizeof(nuint));

        for (var i = 0; i < _count; i++)
        {
            var item = items[i]
                ?? throw new ArgumentException($"Entry at index {i} is null.", parameterName);

            Pointers[i] = item.Length == 0 ? null : (byte*)NativeMemory.Alloc((nuint)item.Length);
            Lengths[i] = (nuint)item.Length;
            if (item.Length > 0)
            {
                item.AsSpan().CopyTo(new Span<byte>(Pointers[i], item.Length));
            }
        }
    }

    internal byte** Pointers { get; private set; }

    internal nuint* Lengths { get; private set; }

    internal nuint Count => (nuint)_count;

    public void Dispose()
    {
        if (Pointers is not null)
        {
            for (var i = 0; i < _count; i++)
            {
                NativeMemory.Free(Pointers[i]);
            }

            NativeMemory.Free(Pointers);
            Pointers = null;
        }

        if (Lengths is not null)
        {
            NativeMemory.Free(Lengths);
            Lengths = null;
        }
    }
}

/// <summary>
/// A <c>const char *const *</c> array of NUL-terminated UTF-8 strings, as the ABI takes for the
/// parallel name/value arrays of <c>prismpdf_document_fill_form</c>.
/// </summary>
internal sealed unsafe class NativeUtf8Array : IDisposable
{
    private readonly int _count;

    internal NativeUtf8Array(IReadOnlyList<string> items, string parameterName)
    {
        Throw.IfNull(items, parameterName);

        _count = items.Count;
        Pointers = (byte**)NativeMemory.Alloc((nuint)Math.Max(_count, 1), (nuint)sizeof(byte*));

        for (var i = 0; i < _count; i++)
        {
            var item = items[i]
                ?? throw new ArgumentException($"Entry at index {i} is null.", parameterName);
            Pointers[i] = Native.AllocUtf8(item);
        }
    }

    internal byte** Pointers { get; private set; }

    internal nuint Count => (nuint)_count;

    public void Dispose()
    {
        if (Pointers is null)
        {
            return;
        }

        for (var i = 0; i < _count; i++)
        {
            Native.FreeUtf8(Pointers[i]);
        }

        NativeMemory.Free(Pointers);
        Pointers = null;
    }
}

/// <summary>
/// A scope owning several NUL-terminated UTF-8 copies for the duration of one native call.
/// </summary>
/// <remarks>
/// <see cref="Native.AllocUtf8"/> plus a <c>try</c>/<c>finally</c> says this for one string. The
/// authoring calls take up to five at once — <c>attach_file</c> has a name, a MIME type, a
/// relationship and an optional description — and nesting four <c>finally</c> blocks around one
/// call obscures the call. Nothing here is library memory: every pointer comes from
/// <see cref="Native.AllocUtf8"/> and goes back to <see cref="Native.FreeUtf8"/>.
/// </remarks>
internal sealed unsafe class Utf8Scope : IDisposable
{
    private readonly List<nint> _allocated = [];

    /// <summary>Copy a required string; a null one is an <see cref="ArgumentNullException"/>.</summary>
    internal byte* Add(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var text = Native.AllocUtf8(value);
        _allocated.Add((nint)text);
        return text;
    }

    /// <summary>Copy an optional string; a null one crosses as a null pointer, as the ABI expects.</summary>
    internal byte* AddOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = Native.AllocUtf8(value);
        _allocated.Add((nint)text);
        return text;
    }

    public void Dispose()
    {
        foreach (var pointer in _allocated)
        {
            Native.FreeUtf8((byte*)pointer);
        }

        _allocated.Clear();
    }
}
