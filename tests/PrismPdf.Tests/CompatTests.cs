using System.Text;
using PrismPdf.Interop;

namespace PrismPdf.Tests;

/// <summary>
/// Covers the substitutes netstandard2.0 forces on the marshalling layer.
/// </summary>
/// <remarks>
/// <para>
/// Targeting netstandard2.0 means hand-writing what the modern BCL provides: the UTF-8
/// conversions (no <c>Marshal.PtrToStringUTF8</c>, no <c>Span</c> overload of
/// <c>Encoding.GetBytes</c>), the unmanaged allocator (no <c>NativeMemory</c>) and the argument
/// guards (no <c>ArgumentNullException.ThrowIfNull</c>). Those are exactly the places a
/// hand-rolled version can be subtly wrong.
/// </para>
/// <para>
/// This fixture is pure managed code, so — like <c>NativeSurfaceTests</c> — it runs on a machine
/// with no Rust toolchain and no native library.
/// </para>
/// </remarks>
[TestFixture]
public sealed unsafe class CompatTests
{
    [TestCase("")]
    [TestCase("invoice.pdf")]
    [TestCase("fattura — €1.234,56")]
    [TestCase("日本語のテキスト")]
    [TestCase("\U0001F4C4 emoji beyond the BMP")]
    public void AllocUtf8_RoundTripsThroughUtf8ToString(string value)
    {
        var buffer = Native.AllocUtf8(value);
        try
        {
            Assert.That(Native.Utf8ToString(buffer), Is.EqualTo(value));
        }
        finally
        {
            Native.FreeUtf8(buffer);
        }
    }

    [Test]
    public void AllocUtf8_NulTerminates_AtTheEncodedLength()
    {
        const string value = "€";  // Three bytes in UTF-8, one UTF-16 code unit.
        var byteCount = Encoding.UTF8.GetByteCount(value);

        var buffer = Native.AllocUtf8(value);
        try
        {
            Assert.That(byteCount, Is.EqualTo(3), "precondition: the char is wider as UTF-8 than as UTF-16");
            Assert.That(buffer[byteCount], Is.EqualTo(0), "the C string is not NUL-terminated");
            Assert.That(new ReadOnlySpan<byte>(buffer, byteCount).ToArray(),
                Is.EqualTo(Encoding.UTF8.GetBytes(value)));
        }
        finally
        {
            Native.FreeUtf8(buffer);
        }
    }

    [Test]
    public void Utf8ToString_NullPointer_IsNull()
    {
        Assert.That(Native.Utf8ToString(null), Is.Null);
    }

    [Test]
    public void NativeMemory_AllocatesWritableMemory_AndFreesIt()
    {
        var block = (byte*)NativeMemory.Alloc(64);
        Assert.That((nint)block, Is.Not.Zero);

        for (var i = 0; i < 64; i++)
        {
            block[i] = (byte)i;
        }

        Assert.That(block[63], Is.EqualTo(63));
        NativeMemory.Free(block);
    }

    [Test]
    public void NativeMemory_ElementOverload_MultipliesTheSize()
    {
        var block = (nuint*)NativeMemory.Alloc(8, (nuint)sizeof(nuint));
        try
        {
            for (var i = 0; i < 8; i++)
            {
                block[i] = (nuint)i;
            }

            Assert.That(block[7], Is.EqualTo((nuint)7));
        }
        finally
        {
            NativeMemory.Free(block);
        }
    }

    [Test]
    public void NativeMemory_FreeOfNull_IsANoOp()
    {
        Assert.DoesNotThrow(() => NativeMemory.Free(null));
    }

    [Test]
    public void ThrowIfNull_ReportsTheCallersParameterName()
    {
        string? password = null;

        var ex = Assert.Throws<ArgumentNullException>(() => Throw.IfNull(password));
        Assert.That(ex!.ParamName, Is.EqualTo(nameof(password)),
            "CallerArgumentExpression is not being applied");
    }

    [Test]
    public void ThrowIfNull_NonNull_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => Throw.IfNull("user"));
    }

    [Test]
    public void ThrowIfNegative_RejectsNegatives_AndAllowsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Throw.IfNegative(-1, "index"));
        Assert.DoesNotThrow(() => Throw.IfNegative(0, "index"));
    }

    [Test]
    public void ThrowIfGreaterThan_RejectsAboveTheBound_AndAllowsIt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Throw.IfGreaterThan(256, byte.MaxValue, "rotation"));
        Assert.DoesNotThrow(() => Throw.IfGreaterThan(255, byte.MaxValue, "rotation"));
    }

    [Test]
    public void ThrowIfDisposed_NamesTheType()
    {
        var ex = Assert.Throws<ObjectDisposedException>(() => Throw.IfDisposed(true, this));
        Assert.That(ex!.ObjectName, Is.EqualTo(typeof(CompatTests).FullName));
    }
}
