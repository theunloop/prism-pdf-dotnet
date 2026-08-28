// The #[repr(C)] enums the shipped surface exposes, projected per rule 9 of the binding author's
// guide: "keep their variant names minus the PrismPdf…_ prefix, as the language's native enum.
// Never renumber; the C values are the contract."
//
// Every value is written out explicitly so that a diff against a newer header is a diff of
// numbers, not of ordering. Enums belonging to areas this SDK does not bind yet (authoring,
// layout, composition, conformance, COS inspection) are deliberately absent — see docs/roadmap.md.

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
