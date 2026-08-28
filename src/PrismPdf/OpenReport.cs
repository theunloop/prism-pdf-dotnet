using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// Whether a document opened strictly or had to be recovered, and what the recovery saw.
/// </summary>
/// <remarks>
/// Recovery is a first-class feature, not a fallback to be ashamed of — real PDFs are frequently
/// broken. This report is how a service tells "this file was fine" from "this file was rebuilt by
/// scanning", which is often worth logging even when the pages come out correct.
/// </remarks>
public sealed unsafe class OpenReport : PrismPdfHandle
{
    internal OpenReport(nint handle)
        : base(handle)
    {
    }

    /// <summary>Whether the strict path succeeded or the document was reconstructed.</summary>
    public OpenMode Mode
    {
        get
        {
            OpenMode mode = default;
            Native.Check(NativeMethods.prismpdf_open_report_mode(Handle, &mode),
                "prismpdf_open_report_mode");
            return mode;
        }
    }

    /// <summary>How many bounded recovery diagnostics the open recorded.</summary>
    public int DiagnosticCount
    {
        get
        {
            nuint count = 0;
            Native.Check(NativeMethods.prismpdf_open_report_diagnostic_count(Handle, &count),
                "prismpdf_open_report_diagnostic_count");
            return Native.ToCount(count);
        }
    }

    /// <summary>Read one recovery diagnostic.</summary>
    /// <param name="index">Zero-based index, below <see cref="DiagnosticCount"/>.</param>
    /// <returns>The reason recovery engaged, and the byte offset it engaged at where known.</returns>
    /// <exception cref="PrismPdfException"><paramref name="index"/> is past the end.</exception>
    public RecoveryDiagnostic Diagnostic(int index)
    {
        RecoveryReason reason = default;
        byte hasOffset = 0;
        nuint offset = 0;

        Native.Check(
            NativeMethods.prismpdf_open_report_diagnostic(
                Handle, Native.ToIndex(index, nameof(index)), &reason, &hasOffset, &offset),
            "prismpdf_open_report_diagnostic");

        return new RecoveryDiagnostic(reason, Native.ToBool(hasOffset) ? offset : null);
    }

    /// <summary>Every recovery diagnostic, in order.</summary>
    /// <returns>A snapshot; the values are plain data and outlive this report.</returns>
    public IReadOnlyList<RecoveryDiagnostic> Diagnostics()
    {
        var count = DiagnosticCount;
        var items = new RecoveryDiagnostic[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = Diagnostic(i);
        }

        return items;
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_open_report_free(handle);
}

/// <summary>One bounded diagnostic from a recovered open.</summary>
/// <param name="Reason">Why the strict open path switched to recovery.</param>
/// <param name="Offset">
/// The byte offset the failure was detected at, or <see langword="null"/> when the diagnostic
/// carries no offset. An offset distinguishes an xref parse failure from catalog-reachability
/// recovery, which has nothing to point at.
/// </param>
public readonly record struct RecoveryDiagnostic(RecoveryReason Reason, nuint? Offset);
