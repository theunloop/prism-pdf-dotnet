using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// A date parsed out of a PDF (§7.9.4).
/// </summary>
/// <remarks>
/// This is deliberately not a <see cref="DateTimeOffset"/>. §7.9.4 permits a date string that
/// declares no relationship to UTC, and there is no honest offset to invent for one — so the
/// offset is optional here, and <see cref="ToDateTimeOffset"/> returns <see langword="null"/>
/// rather than guessing. Use <see cref="ToDateTime"/> when you want the wall-clock reading with
/// <see cref="DateTimeKind.Unspecified"/>.
/// </remarks>
/// <param name="Year">Four-digit year.</param>
/// <param name="Month">Month, 1–12.</param>
/// <param name="Day">Day, 1–31.</param>
/// <param name="Hour">Hour, 0–23.</param>
/// <param name="Minute">Minute, 0–59.</param>
/// <param name="Second">Second, 0–59.</param>
/// <param name="UtcOffsetMinutes">
/// Offset of local time from UTC in minutes (<c>Z</c> is 0), or <see langword="null"/> when the
/// date declares no relationship to UTC.
/// </param>
public readonly record struct PdfDate(
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    int Second,
    int? UtcOffsetMinutes)
{
    internal static PdfDate From(in PrismPdfDate date) => new(
        date.Year,
        date.Month,
        date.Day,
        date.Hour,
        date.Minute,
        date.Second,
        date.HasUtcOffset != 0 ? date.UtcOffsetMinutes : null);

    /// <summary>The wall-clock reading, with <see cref="DateTimeKind.Unspecified"/>.</summary>
    public DateTime ToDateTime() => new(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified);

    /// <summary>
    /// The instant this date names, or <see langword="null"/> when it declares no relationship
    /// to UTC and therefore names no instant.
    /// </summary>
    public DateTimeOffset? ToDateTimeOffset() => UtcOffsetMinutes is { } minutes
        ? new DateTimeOffset(Year, Month, Day, Hour, Minute, Second, TimeSpan.FromMinutes(minutes))
        : null;

    /// <inheritdoc/>
    public override string ToString()
    {
        var stamp = $"{Year:D4}-{Month:D2}-{Day:D2} {Hour:D2}:{Minute:D2}:{Second:D2}";
        if (UtcOffsetMinutes is not { } minutes)
        {
            return stamp;
        }

        var sign = minutes < 0 ? '-' : '+';
        var absolute = Math.Abs(minutes);
        return $"{stamp} {sign}{absolute / 60:D2}:{absolute % 60:D2}";
    }
}

/// <summary>The PDF version a document declares or requires.</summary>
/// <param name="Major">Major version, e.g. 1 or 2.</param>
/// <param name="Minor">Minor version, e.g. 7 or 0.</param>
public readonly record struct PdfVersion(int Major, int Minor)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Major}.{Minor}";
}
