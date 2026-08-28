using PrismPdf.Interop;

namespace PrismPdf;

// The conformance passes. Each one finalises the builder as a document that meets a standard —
// writing XMP, an output intent and a file /ID — or refuses and says which rule was unmet.
//
// A refusal is not a malformed document: it is a document that is fine as PDF and not fine as
// PDF/A or PDF/UA. That is why it raises PrismPdfConformanceException carrying a ConformanceIssue
// rather than the bare status: "the figure has no alternate text" is actionable, "conformance
// failed" is not.
public sealed unsafe partial class Builder
{
    /// <summary>
    /// Finalise this builder as a conformant <b>PDF/A</b> file (§14, ISO 19005): XMP metadata, an
    /// sRGB output intent (§14.11.5) and a file <c>/ID</c>.
    /// </summary>
    /// <remarks>
    /// Fonts must be embedded first — Standard-14 fonts are rejected, which is
    /// <see cref="ConformanceIssue.UnembeddedFont"/>.
    /// </remarks>
    /// <param name="conformance">The level to target.</param>
    /// <param name="metadata">The XMP fields to write.</param>
    /// <exception cref="PrismPdfConformanceException">A rule of the standard is unmet.</exception>
    public void MakePdfA(PdfAConformance conformance, XmpMetadata metadata)
    {
        Throw.IfNull(metadata);

        ConformanceIssue issue = default;
        var status = NativeMethods.prismpdf_builder_make_pdfa(
            Handle, conformance, metadata.Handle, &issue);
        CheckConformance(status, issue, "prismpdf_builder_make_pdfa");
        GC.KeepAlive(metadata);
    }

    /// <summary>
    /// As <see cref="MakePdfA(PdfAConformance, XmpMetadata)"/>, but with a caller-chosen ICC output
    /// intent (§14.11.5) instead of the default sRGB one — a CMYK printing condition, say, so
    /// <c>DeviceCMYK</c> content conforms under PDF/A §6.2.4.3.
    /// </summary>
    /// <param name="conformance">The level to target.</param>
    /// <param name="metadata">The XMP fields to write.</param>
    /// <param name="iccProfile">The ICC profile's bytes.</param>
    /// <param name="componentCount">The profile's colour-component count: 1 Gray, 3 RGB, 4 CMYK.</param>
    /// <param name="identifier">The output-condition identifier.</param>
    /// <exception cref="PrismPdfConformanceException">A rule of the standard is unmet.</exception>
    public void MakePdfA(
        PdfAConformance conformance,
        XmpMetadata metadata,
        ReadOnlySpan<byte> iccProfile,
        int componentCount,
        string identifier)
    {
        Throw.IfNull(metadata);
        Throw.IfNegative(componentCount);

        using var strings = new Utf8Scope();
        var id = strings.Add(identifier, nameof(identifier));

        ConformanceIssue issue = default;
        fixed (byte* icc = iccProfile)
        {
            var status = NativeMethods.prismpdf_builder_make_pdfa_with_output_intent(
                Handle, conformance, metadata.Handle, icc, (nuint)iccProfile.Length,
                (uint)componentCount, id, &issue);
            CheckConformance(status, issue, "prismpdf_builder_make_pdfa_with_output_intent");
        }

        GC.KeepAlive(metadata);
    }

    /// <summary>
    /// Finalise this builder as a conformant <b>PDF/UA-1</b> file (ISO 14289-1): a tagged,
    /// titled, language-tagged document.
    /// </summary>
    /// <param name="metadata">The XMP fields to write.</param>
    /// <param name="lang">The document's natural language, e.g. <c>en-GB</c>.</param>
    /// <exception cref="PrismPdfConformanceException">A rule of the standard is unmet.</exception>
    public void MakePdfUa(XmpMetadata metadata, string lang)
        => MakePdfUa(NativeMethods.prismpdf_builder_make_pdfua, metadata, lang,
            "prismpdf_builder_make_pdfua");

    /// <summary>
    /// Finalise this builder as a conformant <b>PDF/UA-2</b> file (ISO 14289-2), the PDF 2.0
    /// revision — which forbids <c>Note</c> and the generic <c>H</c> heading, and wants structure
    /// destinations on links.
    /// </summary>
    /// <param name="metadata">The XMP fields to write.</param>
    /// <param name="lang">The document's natural language, e.g. <c>en-GB</c>.</param>
    /// <exception cref="PrismPdfConformanceException">A rule of the standard is unmet.</exception>
    public void MakePdfUa2(XmpMetadata metadata, string lang)
        => MakePdfUa(NativeMethods.prismpdf_builder_make_pdfua2, metadata, lang,
            "prismpdf_builder_make_pdfua2");

    private static void CheckConformance(PrismPdfStatus status, ConformanceIssue issue, string operation)
    {
        if (status == PrismPdfStatus.Conformance)
        {
            throw PrismPdfException.CreateConformance(status, operation, issue);
        }

        Native.Check(status, operation);
    }

    private void MakePdfUa(UaPass pass, XmpMetadata metadata, string lang, string operation)
    {
        Throw.IfNull(metadata);

        using var strings = new Utf8Scope();
        var code = strings.Add(lang, nameof(lang));

        ConformanceIssue issue = default;
        CheckConformance(pass(Handle, metadata.Handle, code, &issue), issue, operation);
        GC.KeepAlive(metadata);
    }

    private delegate PrismPdfStatus UaPass(nint builder, nint meta, byte* lang, ConformanceIssue* issue);
}
