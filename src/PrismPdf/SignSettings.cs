using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The optional inputs to a digital signature (§12.8.1): who signed, why, where, whether it is
/// PAdES, whether it is visible, and whether it carries a timestamp.
/// </summary>
/// <remarks>
/// Signing takes more optional inputs than one C signature can carry, so this crosses the ABI as a
/// mutable handle rather than a struct. Build one, pass it to
/// <see cref="Document.Sign(ReadOnlySpan{byte}, ReadOnlySpan{byte}, SignSettings)"/>, and dispose
/// it — signing does <em>not</em> consume it, so the same settings can sign several documents.
/// </remarks>
/// <example>
/// <code>
/// using var settings = new SignSettings
/// {
///     Name = "Ada Lovelace",
///     Reason = "Approved",
///     SigningTime = DateTimeOffset.UtcNow,
/// };
/// var signed = doc.Sign(certificate, key, settings);
/// </code>
/// </example>
public sealed unsafe class SignSettings : PrismPdfHandle
{
    /// <summary>Create empty settings.</summary>
    public SignSettings()
        : base(NativeMethods.prismpdf_sign_settings_new())
    {
    }

    /// <summary>The <c>/Name</c> entry — the signer's name as it should be displayed.</summary>
    public string Name
    {
        set => SetText(NativeMethods.prismpdf_sign_settings_set_name, value,
            "prismpdf_sign_settings_set_name");
    }

    /// <summary>The <c>/Reason</c> entry.</summary>
    public string Reason
    {
        set => SetText(NativeMethods.prismpdf_sign_settings_set_reason, value,
            "prismpdf_sign_settings_set_reason");
    }

    /// <summary>The <c>/Location</c> entry.</summary>
    public string Location
    {
        set => SetText(NativeMethods.prismpdf_sign_settings_set_location, value,
            "prismpdf_sign_settings_set_location");
    }

    /// <summary>The <c>/ContactInfo</c> entry.</summary>
    public string ContactInfo
    {
        set => SetText(NativeMethods.prismpdf_sign_settings_set_contact_info, value,
            "prismpdf_sign_settings_set_contact_info");
    }

    /// <summary>
    /// Pin the signing time instead of reading the clock — what a reproducible build or a
    /// deterministic test needs.
    /// </summary>
    public DateTimeOffset SigningTime
    {
        set => Native.Check(
            NativeMethods.prismpdf_sign_settings_set_signing_time(Handle, (ulong)value.ToUnixTimeSeconds()),
            "prismpdf_sign_settings_set_signing_time");
    }

    /// <summary>Produce a PAdES (ETSI EN 319 142) signature.</summary>
    public bool Pades
    {
        set => Native.Check(
            NativeMethods.prismpdf_sign_settings_set_pades(Handle, Native.FromBool(value)),
            "prismpdf_sign_settings_set_pades");
    }

    /// <summary>
    /// Give the signature a visible widget on a page.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index to place it on.</param>
    /// <param name="rect">Where to place it, in default user space.</param>
    /// <param name="text">A caption, or <see langword="null"/> for an unlabelled box.</param>
    public void SetAppearance(int pageIndex, PdfRect rect, string? text = null)
    {
        var values = stackalloc float[4];
        values[0] = (float)rect.Llx;
        values[1] = (float)rect.Lly;
        values[2] = (float)rect.Urx;
        values[3] = (float)rect.Ury;

        var caption = text is null ? null : Native.AllocUtf8(text);
        try
        {
            Native.Check(
                NativeMethods.prismpdf_sign_settings_set_appearance(
                    Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), values, caption),
                "prismpdf_sign_settings_set_appearance");
        }
        finally
        {
            if (caption is not null)
            {
                Native.FreeUtf8(caption);
            }
        }
    }

    /// <summary>
    /// Embed a signature timestamp (§12.8.3.3), signed by the given timestamp authority.
    /// </summary>
    /// <param name="certificateDer">The TSA's certificate, DER-encoded.</param>
    /// <param name="privateKeyDer">The TSA's private key, DER-encoded.</param>
    /// <param name="generationTime">The time the token asserts.</param>
    /// <param name="serial">The token's serial number.</param>
    public void SetTimestamp(
        ReadOnlySpan<byte> certificateDer,
        ReadOnlySpan<byte> privateKeyDer,
        DateTimeOffset generationTime,
        ulong serial)
    {
        fixed (byte* cert = certificateDer)
        fixed (byte* key = privateKeyDer)
        {
            Native.Check(
                NativeMethods.prismpdf_sign_settings_set_timestamp(
                    Handle, cert, (nuint)certificateDer.Length, key, (nuint)privateKeyDer.Length,
                    (ulong)generationTime.ToUnixTimeSeconds(), serial),
                "prismpdf_sign_settings_set_timestamp");
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_sign_settings_free(handle);

    private delegate PrismPdfStatus TextSetter(nint settings, byte* value);

    private void SetText(TextSetter setter, string value, string operation)
    {
        Throw.IfNull(value);

        var bytes = Native.AllocUtf8(value);
        try
        {
            Native.Check(setter(Handle, bytes), operation);
        }
        finally
        {
            Native.FreeUtf8(bytes);
        }
    }
}
