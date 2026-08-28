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
/// </remarks>
public sealed class PrismPdfException : Exception
{
    private PrismPdfException(PrismPdfStatus status, string message)
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
    public bool HasDiagnostic { get; private init; }

    /// <summary>
    /// Build the exception for a failed call, reading the thread-local diagnostic immediately.
    /// </summary>
    /// <remarks>
    /// This must run on the same thread as, and with no intervening ABI call after, the failure:
    /// the diagnostic slot is thread-local and the next successful guarded call clears it. It is
    /// why every wrapper method calls <c>Native.Check</c> directly on the status it just received,
    /// with no <c>await</c> or continuation in between.
    /// </remarks>
    internal static unsafe PrismPdfException Create(PrismPdfStatus status, string operation)
    {
        nint snapshot = 0;
        var takeStatus = NativeMethods.prismpdf_last_error(&snapshot);

        if (takeStatus != PrismPdfStatus.Ok || snapshot == 0)
        {
            return new PrismPdfException(status, $"{operation} failed: {status}.");
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
                return new PrismPdfException(status, $"{operation} failed: {status}.");
            }

            byte* text = null;
            if (NativeMethods.prismpdf_error_info_message(snapshot, &text) != PrismPdfStatus.Ok)
            {
                return new PrismPdfException(status, $"{operation} failed: {status}.");
            }

            var message = Native.TakeString(text) ?? string.Empty;
            return new PrismPdfException(status, $"{operation} failed: {status}. {message}".TrimEnd())
            {
                HasDiagnostic = message.Length > 0,
            };
        }
        finally
        {
            NativeMethods.prismpdf_error_info_free(snapshot);
        }
    }
}
