using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The bytes a manipulation produced, together with what it cost: which serialization strategy ran,
/// and what happened to any signatures and logical structure in the source.
/// </summary>
/// <remarks>
/// <para>
/// Every <c>…WithReport</c> method on <see cref="Document"/> returns one of these instead of a bare
/// <c>byte[]</c>. The report owns the output bytes, so <see cref="Bytes"/> copies out of a borrowed
/// view; read it before disposing the report.
/// </para>
/// <para>
/// The point of the report is that "the save worked" is not the whole truth. A full rewrite of a
/// signed document produces perfectly valid bytes whose signatures no longer verify —
/// <see cref="SignatureEffect"/> is how you find that out without re-verifying.
/// </para>
/// </remarks>
public sealed unsafe class TransformReport : PrismPdfHandle
{
    internal TransformReport(nint handle)
        : base(handle)
    {
    }

    /// <summary>The serialized PDF, copied out of the report's borrowed byte view.</summary>
    public byte[] Bytes
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_transform_report_bytes(Handle, &data, &len),
                "prismpdf_transform_report_bytes");
            return Native.CopyBorrowedBytes(data, len);
        }
    }

    /// <summary>Which serialization strategy produced <see cref="Bytes"/>.</summary>
    public RewriteMode RewriteMode
    {
        get
        {
            RewriteMode mode = default;
            Native.Check(NativeMethods.prismpdf_transform_report_rewrite_mode(Handle, &mode),
                "prismpdf_transform_report_rewrite_mode");
            return mode;
        }
    }

    /// <summary>What the operation did to signatures already present in the source.</summary>
    public SignatureEffect SignatureEffect
    {
        get
        {
            SignatureEffect effect = default;
            Native.Check(NativeMethods.prismpdf_transform_report_signature_effect(Handle, &effect),
                "prismpdf_transform_report_signature_effect");
            return effect;
        }
    }

    /// <summary>What the operation did to the logical structure tree (§14.7).</summary>
    public StructureEffect StructureEffect
    {
        get
        {
            StructureEffect effect = default;
            Native.Check(NativeMethods.prismpdf_transform_report_structure_effect(Handle, &effect),
                "prismpdf_transform_report_structure_effect");
            return effect;
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_transform_report_free(handle);
}
