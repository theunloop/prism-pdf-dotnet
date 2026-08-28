// The #[repr(C)] value structs that cross by layout. Internal: the idiomatic layer converts them
// to public shapes (PdfDate, OpenOptions) that do not freeze a C struct layout into this SDK's
// public API.

using System.Runtime.InteropServices;

namespace PrismPdf.Interop;

/// <summary>
/// A parsed PDF date (§7.9.4). <c>HasUtcOffset</c> is false when the string declares no
/// relationship to UTC, in which case <c>UtcOffsetMinutes</c> is 0 and carries no meaning.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrismPdfDate
{
    internal ushort Year;
    internal byte Month;
    internal byte Day;
    internal byte Hour;
    internal byte Minute;
    internal byte Second;

    /// <summary>C <c>bool</c> is one byte; 0 is false, non-zero is true.</summary>
    internal byte HasUtcOffset;

    internal short UtcOffsetMinutes;
}

/// <summary>
/// Anti-DoS parsing limits. Zero in any field means "use the default" — 512 for
/// <c>MaxDepth</c>, 2^20 for <c>MaxObjstmObjects</c>, 2^21 for <c>MaxObjects</c>.
/// </summary>
/// <remarks>
/// Deprecated upstream in 0.2.0 in favour of the extensible <c>PrismPdfOpenOptions</c> handle,
/// which is what <see cref="PrismPdf.OpenOptions"/> wraps. Kept bound so the deprecated opener
/// stays reachable from the raw layer for the pre-1.0 compatibility window.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct PrismPdfLimits
{
    internal nuint MaxDepth;
    internal nuint MaxObjstmObjects;
    internal nuint MaxObjects;
}

/// <summary>
/// Page geometry for declarative composition, in PDF points. Reshaped publicly as
/// <see cref="PrismPdf.PdfSize"/> plus <see cref="PrismPdf.PdfMargins"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrismPdfCompositionPageStyle
{
    internal double Width;
    internal double Height;
    internal double MarginLeft;
    internal double MarginRight;
    internal double MarginTop;
    internal double MarginBottom;
}

/// <summary>
/// An RGB colour for declarative composition; every component is in the inclusive range 0–1.
/// Reshaped publicly as <see cref="PrismPdf.PdfColor"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrismPdfCompositionColor
{
    internal double Red;
    internal double Green;
    internal double Blue;
}

/// <summary>
/// Text styling for declarative composition. The idiomatic layer takes the two values as
/// parameters rather than exposing this layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrismPdfCompositionTextStyle
{
    internal double Size;
    internal double Leading;
}
