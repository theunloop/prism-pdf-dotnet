using System.Text;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// One COS object (§7.3) — the expert escape hatch: read any value the document holds, or build a
/// new one to write back through an <see cref="Edit"/>.
/// </summary>
/// <remarks>
/// <para>
/// Everything else in this SDK is a model over the file: pages, annotations, signatures. This is
/// the file itself. Reach for it when the modelled API has no answer — a private dictionary key, a
/// stream nothing else exposes — and prefer the model when it does, because the model keeps the
/// invariants that hand-editing objects can break.
/// </para>
/// <para>
/// Objects are <em>owned</em> and independent: every reader here clones, so an object outlives the
/// document it came from, and disposing one never affects another. Two accessors are the exception
/// and lend rather than copy — <see cref="Bytes"/> and <see cref="StreamRaw"/> — so this wrapper
/// copies those views out before returning them.
/// </para>
/// <para>
/// Accessors are typed: asking an integer for <see cref="Real"/> raises rather than coercing, so
/// check <see cref="Kind"/> first. That is the ABI's rule, not this binding's.
/// </para>
/// </remarks>
public sealed unsafe class PdfObject : PrismPdfHandle
{
    internal PdfObject(nint handle)
        : base(handle)
    {
    }

    /// <summary>Which COS variant this object is (§7.3).</summary>
    public ObjectKind Kind
    {
        get
        {
            ObjectKind kind = default;
            Native.Check(NativeMethods.prismpdf_object_kind(Handle, &kind), "prismpdf_object_kind");
            return kind;
        }
    }

    /// <summary>The boolean value (§7.3.2).</summary>
    /// <exception cref="PrismPdfException">This object is not a boolean.</exception>
    public bool Boolean
    {
        get
        {
            byte value = 0;
            Native.Check(NativeMethods.prismpdf_object_boolean(Handle, &value),
                "prismpdf_object_boolean");
            return Native.ToBool(value);
        }
    }

    /// <summary>The integer value (§7.3.3).</summary>
    /// <exception cref="PrismPdfException">This object is not an integer.</exception>
    public long Integer
    {
        get
        {
            long value = 0;
            Native.Check(NativeMethods.prismpdf_object_integer(Handle, &value),
                "prismpdf_object_integer");
            return value;
        }
    }

    /// <summary>The real value (§7.3.3), without coercing an integer.</summary>
    /// <exception cref="PrismPdfException">This object is not a real.</exception>
    public double Real
    {
        get
        {
            double value = 0;
            Native.Check(NativeMethods.prismpdf_object_real(Handle, &value), "prismpdf_object_real");
            return value;
        }
    }

    /// <summary>
    /// The raw bytes of a string or a name (§7.3.4–§7.3.5), copied out of the lent view.
    /// </summary>
    /// <remarks>
    /// A COS string is bytes, not text: it may be PDFDocEncoded, UTF-16BE with a byte-order mark,
    /// or arbitrary binary. <see cref="Text"/> decodes the common cases; this is what the file
    /// actually holds.
    /// </remarks>
    /// <exception cref="PrismPdfException">This object is neither a string nor a name.</exception>
    public byte[] Bytes
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_object_bytes(Handle, &data, &len),
                "prismpdf_object_bytes");
            return Native.CopyBorrowedBytes(data, len);
        }
    }

    /// <summary>
    /// A string or name decoded as text: UTF-16BE when it carries a byte-order mark, UTF-8
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// A convenience, not an ABI entry point. It is the right reading for a name and for the text
    /// strings §7.9.2 defines; a binary string is better read through <see cref="Bytes"/>.
    /// </remarks>
    /// <exception cref="PrismPdfException">This object is neither a string nor a name.</exception>
    public string Text => Decode(Bytes);

    /// <summary>The object and generation numbers of an indirect reference (§7.3.10).</summary>
    /// <exception cref="PrismPdfException">This object is not a reference.</exception>
    public PdfReference Reference
    {
        get
        {
            uint number = 0;
            ushort generation = 0;
            Native.Check(NativeMethods.prismpdf_object_reference(Handle, &number, &generation),
                "prismpdf_object_reference");
            return new PdfReference((int)number, generation);
        }
    }

    /// <summary>The number of elements in an array (§7.3.6).</summary>
    /// <exception cref="PrismPdfException">This object is not an array.</exception>
    public int ArrayLength
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_object_array_len(Handle, &len),
                "prismpdf_object_array_len");
            return Native.ToCount(len);
        }
    }

    /// <summary>
    /// The number of entries in a dictionary, or in a stream's dictionary (§7.3.7–§7.3.8).
    /// </summary>
    /// <exception cref="PrismPdfException">This object is neither a dictionary nor a stream.</exception>
    public int DictionaryLength
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_object_dictionary_len(Handle, &len),
                "prismpdf_object_dictionary_len");
            return Native.ToCount(len);
        }
    }

    /// <summary>
    /// A stream's raw, still-encoded bytes (§7.3.8), copied out of the lent view.
    /// </summary>
    /// <remarks>
    /// Raw means raw: whatever <c>/Filter</c> the stream declares has <em>not</em> been applied.
    /// Decoded content comes from the modelled API — <c>doc.PageText</c>, a font's program, an
    /// image's samples.
    /// </remarks>
    /// <exception cref="PrismPdfException">This object is not a stream.</exception>
    public byte[] StreamRaw
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_object_stream_raw(Handle, &data, &len),
                "prismpdf_object_stream_raw");
            return Native.CopyBorrowedBytes(data, len);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------------------------

    /// <summary>The null object (§7.3.9).</summary>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewNull() => new(NativeMethods.prismpdf_object_new_null());

    /// <summary>A boolean (§7.3.2).</summary>
    /// <param name="value">The value.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewBoolean(bool value)
        => new(NativeMethods.prismpdf_object_new_boolean(Native.FromBool(value)));

    /// <summary>An integer (§7.3.3).</summary>
    /// <param name="value">The value.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewInteger(long value)
        => new(NativeMethods.prismpdf_object_new_integer(value));

    /// <summary>A real number (§7.3.3). Non-finite values are rejected.</summary>
    /// <param name="value">The value.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewReal(double value)
        => new(NativeMethods.prismpdf_object_new_real(value));

    /// <summary>A binary-safe string (§7.3.4).</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewString(ReadOnlySpan<byte> value)
    {
        fixed (byte* data = value)
        {
            return new PdfObject(NativeMethods.prismpdf_object_new_string(data, (nuint)value.Length));
        }
    }

    /// <inheritdoc cref="NewString(ReadOnlySpan{byte})"/>
    /// <param name="value">The text, encoded as UTF-8 before it crosses.</param>
    public static PdfObject NewString(string value)
    {
        Throw.IfNull(value);
        return NewString(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>A name (§7.3.5), without its leading slash.</summary>
    /// <param name="value">The name's bytes.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewName(ReadOnlySpan<byte> value)
    {
        fixed (byte* data = value)
        {
            return new PdfObject(NativeMethods.prismpdf_object_new_name(data, (nuint)value.Length));
        }
    }

    /// <inheritdoc cref="NewName(ReadOnlySpan{byte})"/>
    /// <param name="value">The name, encoded as UTF-8 before it crosses.</param>
    public static PdfObject NewName(string value)
    {
        Throw.IfNull(value);
        return NewName(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>An indirect reference (§7.3.10).</summary>
    /// <param name="reference">The object and generation numbers.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewReference(PdfReference reference)
        => new(NativeMethods.prismpdf_object_new_reference(
            reference.ToNumber(), reference.ToGeneration()));

    /// <summary>An empty array (§7.3.6).</summary>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewArray() => new(NativeMethods.prismpdf_object_new_array());

    /// <summary>An empty dictionary (§7.3.7).</summary>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewDictionary() => new(NativeMethods.prismpdf_object_new_dictionary());

    /// <summary>
    /// A stream (§7.3.8): a copy of <paramref name="dictionary"/> plus raw, still-encoded bytes.
    /// </summary>
    /// <remarks>
    /// The bytes are stored as given — if the dictionary declares a <c>/Filter</c>, they must
    /// already be encoded with it.
    /// </remarks>
    /// <param name="dictionary">The stream dictionary. Cloned; stays owned by the caller.</param>
    /// <param name="data">The raw stream bytes.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    public static PdfObject NewStream(PdfObject dictionary, ReadOnlySpan<byte> data)
    {
        Throw.IfNull(dictionary);

        fixed (byte* bytes = data)
        {
            var handle = NativeMethods.prismpdf_object_new_stream(
                dictionary.Handle, bytes, (nuint)data.Length);
            GC.KeepAlive(dictionary);
            return new PdfObject(handle);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Containers
    // -----------------------------------------------------------------------------------------

    /// <summary>Clone one array element as an independent object (§7.3.6).</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns>An owned object; dispose it when done.</returns>
    /// <exception cref="PrismPdfException">Not an array, or the index is past the end.</exception>
    public PdfObject ArrayGet(int index)
    {
        nint item = 0;
        Native.Check(
            NativeMethods.prismpdf_object_array_get(Handle, Native.ToIndex(index, nameof(index)), &item),
            "prismpdf_object_array_get");
        return new PdfObject(item);
    }

    /// <summary>Append a copy of a value to an array (§7.3.6).</summary>
    /// <param name="value">The value to clone in. Stays owned by the caller.</param>
    /// <exception cref="PrismPdfException">This object is not an array.</exception>
    public void ArrayPush(PdfObject value)
    {
        Throw.IfNull(value);
        Native.Check(NativeMethods.prismpdf_object_array_push(Handle, value.Handle),
            "prismpdf_object_array_push");
        GC.KeepAlive(value);
    }

    /// <summary>
    /// Look up a dictionary key and clone its value (§7.3.7–§7.3.8). A stream exposes its stream
    /// dictionary through the same call.
    /// </summary>
    /// <param name="key">The key's bytes, without its leading slash.</param>
    /// <returns>
    /// An owned object, or <see langword="null"/> when the dictionary has no such key — absence,
    /// not an error.
    /// </returns>
    /// <exception cref="PrismPdfException">This object is neither a dictionary nor a stream.</exception>
    public PdfObject? DictionaryGet(ReadOnlySpan<byte> key)
    {
        nint value = 0;
        fixed (byte* bytes = key)
        {
            var status = NativeMethods.prismpdf_object_dictionary_get(
                Handle, bytes, (nuint)key.Length, &value);
            if (!Native.CheckOptional(status, "prismpdf_object_dictionary_get"))
            {
                return null;
            }
        }

        return new PdfObject(value);
    }

    /// <inheritdoc cref="DictionaryGet(ReadOnlySpan{byte})"/>
    /// <param name="key">The key, encoded as UTF-8 before it crosses.</param>
    public PdfObject? DictionaryGet(string key)
    {
        Throw.IfNull(key);
        return DictionaryGet(Encoding.UTF8.GetBytes(key));
    }

    /// <summary>Insert a copy of a value under a dictionary key (§7.3.7).</summary>
    /// <param name="key">The key's bytes, without its leading slash.</param>
    /// <param name="value">The value to clone in. Stays owned by the caller.</param>
    /// <exception cref="PrismPdfException">This object is not a dictionary.</exception>
    public void DictionarySet(ReadOnlySpan<byte> key, PdfObject value)
    {
        Throw.IfNull(value);

        fixed (byte* bytes = key)
        {
            Native.Check(
                NativeMethods.prismpdf_object_dictionary_set(
                    Handle, bytes, (nuint)key.Length, value.Handle),
                "prismpdf_object_dictionary_set");
        }

        GC.KeepAlive(value);
    }

    /// <inheritdoc cref="DictionarySet(ReadOnlySpan{byte}, PdfObject)"/>
    /// <param name="key">The key, encoded as UTF-8 before it crosses.</param>
    /// <param name="value">The value to clone in. Stays owned by the caller.</param>
    public void DictionarySet(string key, PdfObject value)
    {
        Throw.IfNull(key);
        DictionarySet(Encoding.UTF8.GetBytes(key), value);
    }

    /// <summary>Clone this object (§7.3).</summary>
    /// <returns>An independently owned copy; dispose it when done.</returns>
    public PdfObject Clone()
    {
        var handle = NativeMethods.prismpdf_object_clone(Handle);
        GC.KeepAlive(this);
        return new PdfObject(handle);
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_object_free(handle);

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>
/// An indirect object's identity: its object number and generation (§7.3.10).
/// </summary>
/// <remarks>
/// The ABI passes these as a <c>uint32_t</c> and a <c>uint16_t</c> side by side. They travel
/// together everywhere they appear, so this SDK carries them together too.
/// </remarks>
/// <param name="Number">The object number.</param>
/// <param name="Generation">The generation number.</param>
public readonly record struct PdfReference(int Number, int Generation)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Number} {Generation} R";

    internal uint ToNumber()
    {
        Throw.IfNegative(Number, nameof(Number));
        return (uint)Number;
    }

    internal ushort ToGeneration()
    {
        Throw.IfNegative(Generation, nameof(Generation));
        Throw.IfGreaterThan(Generation, ushort.MaxValue, nameof(Generation));
        return (ushort)Generation;
    }
}
