using System.Text;
using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// Reusable anti-DoS limits and a password for opening untrusted PDFs.
/// </summary>
/// <remarks>
/// <para>
/// This wraps <c>PrismPdfOpenOptions</c>, the opaque, ABI-extensible replacement for the by-value
/// <c>PrismPdfLimits</c> struct deprecated in core 0.2.0. Because it is opaque, a future option can
/// be added without changing any layout, and one instance can safely be reused for several
/// sequential opens.
/// </para>
/// <para>
/// A limit left unset means "use the engine default": 512 for <see cref="MaxDepth"/>, 2^20 for
/// <see cref="MaxObjstmObjects"/>, 2^21 for <see cref="MaxObjects"/>. Never widen a limit merely
/// to accept one malformed file — raise only the bound a trusted operational requirement
/// implicates, and keep the total object and decompression ceilings.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var options = new OpenOptions { MaxDepth = 64 };
/// using var doc = Document.Open(bytes, options);
/// </code>
/// </example>
public sealed unsafe class OpenOptions : PrismPdfHandle
{
    /// <summary>Create options carrying the engine defaults.</summary>
    public OpenOptions()
        : base(NativeMethods.prismpdf_open_options_new())
    {
    }

    /// <summary>
    /// Maximum object nesting depth before the parser refuses to descend further.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes setters, not getters, for these.</remarks>
    public nuint MaxDepth
    {
        set => Native.Check(NativeMethods.prismpdf_open_options_set_max_depth(Handle, value),
            "prismpdf_open_options_set_max_depth");
    }

    /// <summary>Maximum objects declared by a single object stream's <c>/N</c> (§7.5.7).</summary>
    public nuint MaxObjstmObjects
    {
        set => Native.Check(NativeMethods.prismpdf_open_options_set_max_objstm_objects(Handle, value),
            "prismpdf_open_options_set_max_objstm_objects");
    }

    /// <summary>Maximum objects in the whole document.</summary>
    public nuint MaxObjects
    {
        set => Native.Check(NativeMethods.prismpdf_open_options_set_max_objects(Handle, value),
            "prismpdf_open_options_set_max_objects");
    }

    /// <summary>
    /// The password to try, as both user and owner password (§7.6).
    /// </summary>
    /// <remarks>
    /// The setter copies the bytes, so the caller's buffer may be cleared immediately afterwards.
    /// </remarks>
    /// <param name="password">The password bytes.</param>
    public void SetPassword(ReadOnlySpan<byte> password)
    {
        fixed (byte* bytes = password)
        {
            Native.Check(
                NativeMethods.prismpdf_open_options_set_password(Handle, bytes, (nuint)password.Length),
                "prismpdf_open_options_set_password");
        }
    }

    /// <inheritdoc cref="SetPassword(ReadOnlySpan{byte})"/>
    public void SetPassword(string password)
    {
        Throw.IfNull(password);
        SetPassword(Encoding.UTF8.GetBytes(password));
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_open_options_free(handle);
}
