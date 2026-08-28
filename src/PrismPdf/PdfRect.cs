namespace PrismPdf;

/// <summary>
/// A PDF rectangle in default user space, in the order the ABI hands it over: lower-left corner
/// then upper-right corner (§7.9.5).
/// </summary>
/// <param name="Llx">Lower-left x.</param>
/// <param name="Lly">Lower-left y.</param>
/// <param name="Urx">Upper-right x.</param>
/// <param name="Ury">Upper-right y.</param>
public readonly record struct PdfRect(double Llx, double Lly, double Urx, double Ury)
{
    /// <summary>Width, as the absolute horizontal extent.</summary>
    public double Width => Math.Abs(Urx - Llx);

    /// <summary>Height, as the absolute vertical extent.</summary>
    public double Height => Math.Abs(Ury - Lly);

    /// <inheritdoc/>
    public override string ToString() => $"[{Llx} {Lly} {Urx} {Ury}]";
}

/// <summary>
/// A page size in points, in the order the ABI takes it: width then height.
/// </summary>
/// <param name="Width">Page width in points.</param>
/// <param name="Height">Page height in points.</param>
public readonly record struct PdfSize(double Width, double Height)
{
    /// <summary>US Letter, 612 × 792 points — the engine's default page size.</summary>
    public static PdfSize Letter => new(612, 792);

    /// <summary>ISO A4, 595 × 842 points.</summary>
    public static PdfSize A4 => new(595, 842);

    /// <summary>This size with width and height exchanged.</summary>
    /// <returns>The landscape form of a portrait size, and vice versa.</returns>
    public PdfSize Rotate() => new(Height, Width);

    /// <inheritdoc/>
    public override string ToString() => $"{Width} × {Height}";
}

/// <summary>
/// Page margins in points, in the order the ABI takes them: top, right, bottom, left.
/// </summary>
/// <param name="Top">Top margin in points.</param>
/// <param name="Right">Right margin in points.</param>
/// <param name="Bottom">Bottom margin in points.</param>
/// <param name="Left">Left margin in points.</param>
public readonly record struct PdfMargins(double Top, double Right, double Bottom, double Left)
{
    /// <summary>The same margin on all four sides.</summary>
    /// <param name="all">The margin in points.</param>
    /// <returns>Uniform margins.</returns>
    public static PdfMargins Uniform(double all) => new(all, all, all, all);

    /// <inheritdoc/>
    public override string ToString() => $"[{Top} {Right} {Bottom} {Left}]";
}

/// <summary>
/// An RGB colour with components in the inclusive range 0–1, as declarative composition takes
/// them.
/// </summary>
/// <param name="Red">Red, 0 to 1.</param>
/// <param name="Green">Green, 0 to 1.</param>
/// <param name="Blue">Blue, 0 to 1.</param>
public readonly record struct PdfColor(double Red, double Green, double Blue)
{
    /// <summary>Black.</summary>
    public static PdfColor Black => new(0, 0, 0);

    /// <summary>White.</summary>
    public static PdfColor White => new(1, 1, 1);

    /// <summary>A colour from three 0–255 channel values.</summary>
    /// <param name="red">Red, 0 to 255.</param>
    /// <param name="green">Green, 0 to 255.</param>
    /// <param name="blue">Blue, 0 to 255.</param>
    /// <returns>The colour, with each channel divided by 255.</returns>
    public static PdfColor FromBytes(byte red, byte green, byte blue)
        => new(red / 255.0, green / 255.0, blue / 255.0);

    /// <inheritdoc/>
    public override string ToString() => $"rgb({Red}, {Green}, {Blue})";
}
