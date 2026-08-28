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
