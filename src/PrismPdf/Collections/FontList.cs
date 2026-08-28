using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>
/// Every font a document's pages reference (§9.5–§9.7, §9.9), with its embedded program where one
/// is present.
/// </summary>
public sealed unsafe class FontList : NativeList<Font>
{
    internal FontList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_font_list_len(Handle, &len), "prismpdf_font_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override Font this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_font_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_font_list_get");
            return new Font(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_font_list_free(handle);
}

/// <summary>One referenced font, borrowed from its <see cref="FontList"/>.</summary>
public sealed unsafe class Font : BorrowedItem
{
    internal Font(FontList owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary><c>/BaseFont</c>, often carrying a subset tag such as <c>ABCDEF+</c>.</summary>
    public string BaseFont
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_font_base_font(Item, &text), "prismpdf_font_base_font");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary><c>/Subtype</c> — <c>Type1</c>, <c>TrueType</c>, <c>Type0</c>, ….</summary>
    public string Subtype
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_font_subtype(Item, &text), "prismpdf_font_subtype");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary>
    /// The embedded program's format, or <see langword="null"/> when the font is not embedded.
    /// </summary>
    /// <remarks>
    /// Null here is the PDF/A pre-flight check: an unembedded font is what
    /// <c>ConformanceIssue.UnembeddedFont</c> reports.
    /// </remarks>
    public FontFormat? ProgramFormat
    {
        get
        {
            FontFormat format = default;
            return Native.CheckOptional(NativeMethods.prismpdf_font_program_format(Item, &format),
                "prismpdf_font_program_format")
                ? format
                : null;
        }
    }

    /// <summary>
    /// The embedded program bytes copied out of the list's allocation, or <see langword="null"/>
    /// when the font is not embedded.
    /// </summary>
    public byte[]? Program
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_font_program(Item, &data, &len),
                "prismpdf_font_program")
                ? Native.CopyBorrowedBytes(data, len)
                : null;
        }
    }

    /// <summary>
    /// Parsed sfnt metrics, or <see langword="null"/> for Type1/CFF and unparseable programs.
    /// </summary>
    public FontMetrics? Metrics
    {
        get
        {
            ushort unitsPerEm = 0;
            ushort glyphCount = 0;
            return Native.CheckOptional(
                NativeMethods.prismpdf_font_metrics(Item, &unitsPerEm, &glyphCount), "prismpdf_font_metrics")
                ? new FontMetrics(unitsPerEm, glyphCount)
                : null;
        }
    }

    /// <summary>The family name read from the program, or <see langword="null"/> when unavailable.</summary>
    public string? FamilyName
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_font_family_name(Item, &text),
                "prismpdf_font_family_name")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{BaseFont} ({Subtype}{(ProgramFormat is { } f ? $", {f}" : ", not embedded")})";
}

/// <summary>Metrics parsed out of an embedded sfnt program.</summary>
/// <param name="UnitsPerEm">The font's design grid, e.g. 1000 or 2048.</param>
/// <param name="GlyphCount">How many glyphs the program contains.</param>
public readonly record struct FontMetrics(int UnitsPerEm, int GlyphCount);
