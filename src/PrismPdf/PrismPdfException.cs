using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The single exception type this SDK raises for a non-<see cref="PrismPdfStatus.Ok"/> status.
/// </summary>
/// <remarks>
/// <para>
/// Semantic contract 1 of the binding author's guide: <em>one error type</em>. Every failed ABI
/// call surfaces as this exception carrying the stable integer <see cref="Status"/> and, where the
/// library had one to give, the diagnostic message from <c>prismpdf_last_error</c>.
/// </para>
/// <para>
/// <see cref="PrismPdfStatus.NotFound"/> from an <em>optional</em> getter is absence, not failure,
/// and never reaches this type — those members return <see langword="null"/>. <c>NotFound</c> from
/// an index lookup (a page index past the end) is still an error and does throw.
/// </para>
/// <para>
/// One status carries more than a code: <see cref="PrismPdfStatus.Conformance"/> names the rule
/// that was unmet, so it arrives as <see cref="PrismPdfConformanceException"/> — a subtype, so
/// <c>catch (PrismPdfException)</c> still catches every failure this SDK raises.
/// </para>
/// </remarks>
public class PrismPdfException : Exception
{
    private protected PrismPdfException(PrismPdfStatus status, string message)
        : base(message)
    {
        Status = status;
    }

    /// <summary>The stable status code the failing ABI call returned.</summary>
    public PrismPdfStatus Status { get; }

    /// <summary>
    /// Whether the message came from the library's diagnostic slot rather than being synthesised
    /// from the status name alone. False for pure argument-check rejections, which by design do
    /// not refresh the slot.
    /// </summary>
    public bool HasDiagnostic { get; private protected init; }

    /// <summary>
    /// Build the exception for a failed call, reading the thread-local diagnostic immediately.
    /// </summary>
    /// <remarks>
    /// This must run on the same thread as, and with no intervening ABI call after, the failure:
    /// the diagnostic slot is thread-local and the next successful guarded call clears it. It is
    /// why every wrapper method calls <c>Native.Check</c> directly on the status it just received,
    /// with no <c>await</c> or continuation in between.
    /// </remarks>
    internal static PrismPdfException Create(PrismPdfStatus status, string operation)
    {
        var (message, hasDiagnostic) = Describe(status, operation);
        return new PrismPdfException(status, message) { HasDiagnostic = hasDiagnostic };
    }

    /// <summary>
    /// Build the exception for a refused conformance pass, which reports <em>which</em> rule was
    /// unmet alongside the status.
    /// </summary>
    internal static PrismPdfConformanceException CreateConformance(
        PrismPdfStatus status,
        string operation,
        ConformanceIssue issue)
    {
        var (message, hasDiagnostic) = Describe(status, operation);
        return new PrismPdfConformanceException(status, $"{message} ({issue})", issue)
        {
            HasDiagnostic = hasDiagnostic,
        };
    }

    /// <summary>
    /// Read the library's diagnostic for the call that just failed, and phrase it.
    /// </summary>
    /// <remarks>
    /// This must run on the same thread as, and with no intervening ABI call after, the failure:
    /// the diagnostic slot is thread-local and the next successful guarded call clears it. It is
    /// why every wrapper method calls <c>Native.Check</c> directly on the status it just received,
    /// with no <c>await</c> or continuation in between.
    /// </remarks>
    private static unsafe (string Message, bool HasDiagnostic) Describe(
        PrismPdfStatus status,
        string operation)
    {
        var fallback = $"{operation} failed: {status}.";

        nint snapshot = 0;
        var takeStatus = NativeMethods.prismpdf_last_error(&snapshot);

        if (takeStatus != PrismPdfStatus.Ok || snapshot == 0)
        {
            return (fallback, false);
        }

        try
        {
            // A pure argument-check rejection reports its status directly and deliberately leaves
            // the slot holding an earlier, unrelated failure. Only trust the message when the
            // snapshot describes the call that just failed.
            PrismPdfStatus snapshotStatus = default;
            if (NativeMethods.prismpdf_error_info_status(snapshot, &snapshotStatus) != PrismPdfStatus.Ok
                || snapshotStatus != status)
            {
                return (fallback, false);
            }

            byte* text = null;
            if (NativeMethods.prismpdf_error_info_message(snapshot, &text) != PrismPdfStatus.Ok)
            {
                return (fallback, false);
            }

            var message = Native.TakeString(text) ?? string.Empty;
            return ($"{fallback} {message}".TrimEnd(), message.Length > 0);
        }
        finally
        {
            NativeMethods.prismpdf_error_info_free(snapshot);
        }
    }
}

/// <summary>
/// A conformance pass refused the document: nothing is malformed, a standard's rule is unmet.
/// </summary>
/// <remarks>
/// This is the one failure whose status alone is not actionable. <see cref="Issue"/> names the rule
/// — "the figure has no alternate text", not "conformance failed" — so a caller can fix the
/// document rather than guess. Raised only by the <c>MakePdfA</c> and <c>MakePdfUa</c> family.
/// </remarks>
public sealed class PrismPdfConformanceException : PrismPdfException
{
    internal PrismPdfConformanceException(
        PrismPdfStatus status,
        string message,
        ConformanceIssue issue)
        : base(status, message)
    {
        Issue = issue;
    }

    /// <summary>Which rule the document did not meet.</summary>
    public ConformanceIssue Issue { get; }
}
