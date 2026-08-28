// The #[repr(C)] enums the shipped surface exposes, projected per rule 9 of the binding author's
// guide: "keep their variant names minus the PrismPdf…_ prefix, as the language's native enum.
// Never renumber; the C values are the contract."
//
// Every value is written out explicitly so that a diff against a newer header is a diff of
// numbers, not of ordering. Every #[repr(C)] enum in the vendored header is projected here.

namespace PrismPdf;

/// <summary>
/// Result status for every C ABI call. <see cref="Ok"/> is 0; the non-zero values are stable
/// integer codes that the ABI never renumbers, so it is safe to switch on them.
/// </summary>
public enum PrismPdfStatus
{
    /// <summary>Success.</summary>
    Ok = 0,

    /// <summary>A required pointer argument was null.</summary>
    NullArgument = 1,

    /// <summary>The document could not be parsed, even after recovery.</summary>
    Parse = 2,

    /// <summary>
    /// The requested item does not exist. On an optional getter this is absence, and the SDK
    /// returns <see langword="null"/> rather than throwing; on an index lookup it is an error.
    /// </summary>
    NotFound = 3,

    /// <summary>An internal error — including a caught Rust panic — occurred.</summary>
    Internal = 4,

    /// <summary>The document is encrypted and the supplied password is wrong (§7.6).</summary>
    Password = 5,

    /// <summary>
    /// A conformance pass refused the document: nothing is malformed, a standard's rule is unmet.
    /// </summary>
    Conformance = 6,

    /// <summary>A handle is stale, belongs to a released tree, or has already been finalised.</summary>
    InvalidUse = 7,

    /// <summary>Declarative composition rejected geometry or could not paginate the element tree.</summary>
    Layout = 8,
}

/// <summary>How a document was opened.</summary>
public enum OpenMode
{
    /// <summary>The cross-reference parsed cleanly and the catalog was reachable.</summary>
    Strict = 0,

    /// <summary>The strict path failed and the document was reconstructed by scanning.</summary>
    Recovered = 1,
}

/// <summary>Why the strict open path switched to recovery.</summary>
public enum RecoveryReason
{
    /// <summary>The cross-reference table or stream could not be parsed.</summary>
    XrefParseFailure = 0,

    /// <summary>The cross-reference parsed, but the catalog could not be reached through it.</summary>
    UnreachableCatalog = 1,
}

/// <summary>The serialization strategy a manipulation used.</summary>
public enum RewriteMode
{
    /// <summary>A new revision was appended; every original byte is retained (§7.5.6).</summary>
    Incremental = 0,

    /// <summary>The live object graph was re-emitted as one normalized revision.</summary>
    FullRewrite = 1,

    /// <summary>The document was rebuilt from a scan because its cross-reference was unusable.</summary>
    Reconstructed = 2,
}

/// <summary>What a manipulation did to signatures already present in the source.</summary>
public enum SignatureEffect
{
    /// <summary>Existing signed byte ranges still cover what they claim.</summary>
    Preserved = 0,

    /// <summary>The signatures survive in the file but no longer verify.</summary>
    Invalidated = 1,

    /// <summary>The signatures are not present in the output at all.</summary>
    Removed = 2,
}

/// <summary>What a manipulation did to the logical structure tree (§14.7).</summary>
public enum StructureEffect
{
    /// <summary>The structure tree came through intact.</summary>
    Preserved = 0,

    /// <summary>The output carries no structure tree.</summary>
    Removed = 1,

    /// <summary>A structure tree is present but no longer describes the content correctly.</summary>
    Invalidated = 2,
}

/// <summary>The format of an embedded font program (§9.9).</summary>
public enum FontFormat
{
    /// <summary><c>/FontFile</c> — a Type 1 font program.</summary>
    Type1 = 0,

    /// <summary><c>/FontFile2</c> — a TrueType (sfnt) font program.</summary>
    TrueType = 1,

    /// <summary><c>/FontFile3</c> with <c>/Subtype /Type1C</c> or <c>/CIDFontType0C</c> — bare CFF.</summary>
    Cff = 2,

    /// <summary><c>/FontFile3</c> with <c>/Subtype /OpenType</c> — an OpenType (sfnt) program.</summary>
    OpenType = 3,
}

/// <summary>An extracted image's colour space (§8.6).</summary>
public enum ColorSpace
{
    /// <summary><c>/DeviceGray</c> — one component per sample.</summary>
    DeviceGray = 0,

    /// <summary><c>/DeviceRGB</c> — three components per sample.</summary>
    DeviceRgb = 1,

    /// <summary><c>/DeviceCMYK</c> — four components per sample.</summary>
    DeviceCmyk = 2,

    /// <summary>
    /// Anything else. Read <see cref="Collections.Image.Components"/> to size a sample — it is the only way
    /// to walk the bytes of an <see cref="Other"/> space.
    /// </summary>
    Other = 3,
}

/// <summary>How an extracted image's payload is encoded.</summary>
public enum ImageKind
{
    /// <summary>Decoded raster samples, row-major.</summary>
    Raw = 0,

    /// <summary>A complete JPEG file (<c>DCTDecode</c>), verbatim.</summary>
    Jpeg = 1,

    /// <summary>A complete JPEG 2000 file (<c>JPXDecode</c>), verbatim.</summary>
    Jpeg2000 = 2,

    /// <summary>An undecodable JBIG2 codestream (<c>JBIG2Decode</c>), verbatim.</summary>
    Jbig2 = 3,
}

/// <summary>
/// Whether a signed document's certificate-chain revocation state could be established (§12.8.4).
/// </summary>
public enum Revocation
{
    /// <summary>Every non-anchor link is covered by verified material and none is revoked.</summary>
    Good = 0,

    /// <summary>At least one link in the chain is revoked.</summary>
    Revoked = 1,

    /// <summary>
    /// Nothing is revoked, but at least one link has no usable material — the long-term claim is
    /// incomplete.
    /// </summary>
    Incomplete = 2,
}

/// <summary>
/// The encryption algorithm passed to the <c>SaveEncrypted…</c> family. The ABI takes a plain
/// <c>uint32_t</c> here rather than a <c>#[repr(C)]</c> enum; these are the values
/// <c>docs/ABI.md</c> defines for it, and any other value is rejected.
/// </summary>
public enum EncryptionAlgorithm
{
    /// <summary>RC4 with a 128-bit key. Present for compatibility with old readers only.</summary>
    Rc4128 = 0,

    /// <summary>AES-128 in CBC mode. The ABI's default.</summary>
    Aes128 = 1,

    /// <summary>AES-256 in CBC mode (PDF 2.0, ISO/TS 32003).</summary>
    Aes256 = 2,

    /// <summary>AES-256 in GCM mode — authenticated encryption.</summary>
    Aes256Gcm = 3,
}

/// <summary>
/// One of the fourteen Standard-14 fonts (§9.6.2.2) a page may name in its resources without
/// embedding anything.
/// </summary>
/// <remarks>
/// These are only for drafts and PDF/A-exempt output: PDF/A and PDF/UA both require every font to
/// be embedded, so a document destined for either must use an embedded font instead.
/// </remarks>
public enum StdFont
{
    /// <summary>Helvetica.</summary>
    Helvetica = 0,

    /// <summary>Helvetica Bold.</summary>
    HelveticaBold = 1,

    /// <summary>Helvetica Oblique.</summary>
    HelveticaOblique = 2,

    /// <summary>Helvetica Bold Oblique.</summary>
    HelveticaBoldOblique = 3,

    /// <summary>Times Roman.</summary>
    TimesRoman = 4,

    /// <summary>Times Bold.</summary>
    TimesBold = 5,

    /// <summary>Times Italic.</summary>
    TimesItalic = 6,

    /// <summary>Times Bold Italic.</summary>
    TimesBoldItalic = 7,

    /// <summary>Courier.</summary>
    Courier = 8,

    /// <summary>Courier Bold.</summary>
    CourierBold = 9,

    /// <summary>Courier Oblique.</summary>
    CourierOblique = 10,

    /// <summary>Courier Bold Oblique.</summary>
    CourierBoldOblique = 11,

    /// <summary>Symbol.</summary>
    Symbol = 12,

    /// <summary>Zapf Dingbats.</summary>
    ZapfDingbats = 13,
}

/// <summary>Horizontal alignment of a text block (§9.4.3).</summary>
public enum Align
{
    /// <summary>Ragged right.</summary>
    Left = 0,

    /// <summary>Centred.</summary>
    Center = 1,

    /// <summary>Ragged left.</summary>
    Right = 2,

    /// <summary>Flush both margins.</summary>
    Justify = 3,
}

/// <summary>How list items are marked.</summary>
public enum ListStyle
{
    /// <summary>A bullet before each item.</summary>
    Bullet = 0,

    /// <summary>An incrementing number before each item.</summary>
    Numbered = 1,
}

/// <summary>
/// Logical structure roles without associated string or numeric data (§14.7–§14.8), as declarative
/// composition names them.
/// </summary>
public enum CompositionSemantic
{
    /// <summary>A paragraph (<c>P</c>).</summary>
    Paragraph = 0,

    /// <summary>A list (<c>L</c>).</summary>
    List = 1,

    /// <summary>A list item (<c>LI</c>).</summary>
    ListItem = 2,

    /// <summary>A list item's label (<c>Lbl</c>).</summary>
    ListLabel = 3,

    /// <summary>A list item's body (<c>LBody</c>).</summary>
    ListBody = 4,

    /// <summary>A table (<c>Table</c>).</summary>
    Table = 5,

    /// <summary>A table row (<c>TR</c>).</summary>
    TableRow = 6,

    /// <summary>A table header cell (<c>TH</c>).</summary>
    TableHeaderCell = 7,

    /// <summary>A table data cell (<c>TD</c>).</summary>
    TableCell = 8,
}

/// <summary>Image scaling policy inside the requested composition box (§8.9).</summary>
public enum CompositionImageSizing
{
    /// <summary>Scale down to fit inside the box, preserving aspect ratio.</summary>
    Fit = 0,

    /// <summary>Cover the box, preserving aspect ratio; overflow is clipped.</summary>
    Fill = 1,

    /// <summary>Use the box exactly, whatever that does to the aspect ratio.</summary>
    Exact = 2,
}

/// <summary>Horizontal alignment inside a constrained composition box.</summary>
public enum CompositionHorizontalAlign
{
    /// <summary>Against the left edge.</summary>
    Left = 0,

    /// <summary>Centred horizontally.</summary>
    Center = 1,

    /// <summary>Against the right edge.</summary>
    Right = 2,
}

/// <summary>Vertical alignment inside a constrained composition box.</summary>
public enum CompositionVerticalAlign
{
    /// <summary>Against the top edge.</summary>
    Top = 0,

    /// <summary>Centred vertically.</summary>
    Center = 1,

    /// <summary>Against the bottom edge.</summary>
    Bottom = 2,
}

/// <summary>
/// A PDF/A conformance level (§14, ISO 19005). Part and level together: <c>A2u</c> is part 2,
/// level U.
/// </summary>
public enum PdfAConformance
{
    /// <summary>PDF/A-1b — basic, ISO 19005-1.</summary>
    A1b = 0,

    /// <summary>PDF/A-1a — accessible (requires tagging).</summary>
    A1a = 1,

    /// <summary>PDF/A-2b — basic, ISO 19005-2.</summary>
    A2b = 2,

    /// <summary>PDF/A-2u — basic plus Unicode mapping.</summary>
    A2u = 3,

    /// <summary>PDF/A-2a — accessible.</summary>
    A2a = 4,

    /// <summary>PDF/A-3b — basic, permits attachments.</summary>
    A3b = 5,

    /// <summary>PDF/A-3u — plus Unicode mapping.</summary>
    A3u = 6,

    /// <summary>PDF/A-3a — accessible.</summary>
    A3a = 7,

    /// <summary>PDF/A-4 — ISO 19005-4, on PDF 2.0.</summary>
    A4 = 8,

    /// <summary>PDF/A-4e — engineering.</summary>
    A4e = 9,

    /// <summary>PDF/A-4f — permits attachments.</summary>
    A4f = 10,
}

/// <summary>
/// Why a conformance pass refused the document — the payload of
/// <see cref="PrismPdfConformanceException"/>.
/// </summary>
public enum ConformanceIssue
{
    /// <summary>
    /// A font is not embedded (PDF/A §6.3.4, PDF/UA §7.21.4.1). Standard-14 fonts are rejected:
    /// embed a real program.
    /// </summary>
    UnembeddedFont = 0,

    /// <summary>The document has attachments, which only PDF/A-3 and PDF/A-4f permit (§6.8).</summary>
    AttachmentRequiresPdfA3 = 1,

    /// <summary>Level A conformance requires logical structure — the document is untagged (§6.9).</summary>
    LevelARequiresTagging = 2,

    /// <summary>The content uses transparency, which PDF/A-1 forbids (§6.4).</summary>
    TransparencyRequiresPdfA2 = 3,

    /// <summary>PDF/UA requires a tagged document (14289-1 §7.1).</summary>
    NotTagged = 4,

    /// <summary>PDF/UA requires a document title (14289-1 §7.1, with <c>/DisplayDocTitle</c>).</summary>
    MissingTitle = 5,

    /// <summary>PDF/UA requires a natural language (<c>/Lang</c>, 14289-1 §7.2).</summary>
    MissingLanguage = 6,

    /// <summary>A figure has no alternative description (14289-1 §7.3).</summary>
    FigureWithoutAlt = 7,

    /// <summary>PDF/UA-2 forbids <c>Note</c>; use <c>FENote</c> (14289-2 §8.2.5.14).</summary>
    NoteForbidden = 8,

    /// <summary>PDF/UA-2 forbids the generic <c>H</c> heading (14289-2 §8.2.5.12).</summary>
    GenericHeadingForbidden = 9,

    /// <summary>An embedded file has no description (14289-2 §8.14.1).</summary>
    AttachmentWithoutDesc = 10,

    /// <summary>A link has no structure destination (14289-2 §8.9.2).</summary>
    LinkWithoutStructureDest = 11,

    /// <summary>A structure element uses a type outside the declared namespace.</summary>
    UnknownStructureType = 12,

    /// <summary>The content references the <c>.notdef</c> glyph (14289-1 §7.21.4.2).</summary>
    NotdefGlyph = 13,
}

/// <summary>The exact COS variant stored in a <see cref="PdfObject"/> (§7.3.2–§7.3.10).</summary>
public enum ObjectKind
{
    /// <summary>The null object (§7.3.9).</summary>
    Null = 0,

    /// <summary>A boolean (§7.3.2).</summary>
    Boolean = 1,

    /// <summary>An integer (§7.3.3).</summary>
    Integer = 2,

    /// <summary>A real number (§7.3.3).</summary>
    Real = 3,

    /// <summary>A string (§7.3.4) — bytes, not text.</summary>
    String = 4,

    /// <summary>A name (§7.3.5).</summary>
    Name = 5,

    /// <summary>An array (§7.3.6).</summary>
    Array = 6,

    /// <summary>A dictionary (§7.3.7).</summary>
    Dictionary = 7,

    /// <summary>A stream (§7.3.8) — a dictionary plus encoded bytes.</summary>
    Stream = 8,

    /// <summary>An indirect reference (§7.3.10).</summary>
    Reference = 9,
}

/// <summary>How an object-edit transaction is committed (§7.5–§7.5.6).</summary>
public enum EditCommitMode
{
    /// <summary>
    /// Append a new revision while retaining every original byte — which is what keeps an existing
    /// signature's byte range intact.
    /// </summary>
    Incremental = 0,

    /// <summary>Re-emit the live object graph as one normalized revision.</summary>
    FullRewrite = 1,
}
