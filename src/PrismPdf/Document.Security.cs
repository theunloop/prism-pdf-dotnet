using System.Text;
using PrismPdf.Collections;
using PrismPdf.Interop;

namespace PrismPdf;

// Encryption (§7.6), signing (§12.8) and verification (§12.8, §12.8.4).
public sealed unsafe partial class Document
{
    // -------------------------------------------------------------------------------------------
    // Encryption (§7.6)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Encrypted full rewrite granting every permission (§7.6).
    /// </summary>
    /// <param name="userPassword">The password that opens the document for reading.</param>
    /// <param name="ownerPassword">
    /// The password that grants full access. Empty defaults to <paramref name="userPassword"/>.
    /// </param>
    /// <param name="algorithm">The cipher to use.</param>
    /// <returns>The encrypted PDF.</returns>
    public byte[] SaveEncrypted(
        string userPassword,
        string ownerPassword = "",
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes128)
    {
        Throw.IfNull(userPassword);
        Throw.IfNull(ownerPassword);
        return SaveEncrypted(
            Encoding.UTF8.GetBytes(userPassword), Encoding.UTF8.GetBytes(ownerPassword), algorithm);
    }

    /// <inheritdoc cref="SaveEncrypted(string, string, EncryptionAlgorithm)"/>
    public byte[] SaveEncrypted(
        ReadOnlySpan<byte> userPassword,
        ReadOnlySpan<byte> ownerPassword,
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes128)
    {
        fixed (byte* user = userPassword)
        fixed (byte* owner = ownerPassword)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_save_encrypted(
                    Handle, user, (nuint)userPassword.Length, owner, (nuint)ownerPassword.Length,
                    (uint)algorithm, &data, &len),
                "prismpdf_document_save_encrypted");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>
    /// Encrypted full rewrite with explicit permissions — the complete form of
    /// <see cref="SaveEncrypted(string, string, EncryptionAlgorithm)"/>, which always grants
    /// everything.
    /// </summary>
    /// <param name="userPassword">The password that opens the document for reading.</param>
    /// <param name="ownerPassword">The password that grants full access; empty means the user password.</param>
    /// <param name="permissions">What the user password is allowed to do (§7.6.3.2).</param>
    /// <param name="encryptMetadata">
    /// Whether to encrypt <c>/Metadata</c>. Pass <see langword="false"/> to leave it in clear text,
    /// as PDF/A requires.
    /// </param>
    /// <param name="algorithm">The cipher to use.</param>
    /// <returns>The encrypted PDF.</returns>
    public byte[] SaveEncrypted(
        ReadOnlySpan<byte> userPassword,
        ReadOnlySpan<byte> ownerPassword,
        Permissions permissions,
        bool encryptMetadata,
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes128)
    {
        fixed (byte* user = userPassword)
        fixed (byte* owner = ownerPassword)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_save_encrypted_with(
                    Handle, user, (nuint)userPassword.Length, owner, (nuint)ownerPassword.Length,
                    permissions.Value, Native.FromBool(encryptMetadata), (uint)algorithm, &data, &len),
                "prismpdf_document_save_encrypted_with");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>
    /// As <see cref="SaveEncrypted(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Permissions, bool, EncryptionAlgorithm)"/>,
    /// plus a PDF MAC (ISO/TS 32004) so tampering becomes detectable rather than merely
    /// undecryptable.
    /// </summary>
    /// <param name="userPassword">The password that opens the document for reading.</param>
    /// <param name="ownerPassword">The password that grants full access; empty means the user password.</param>
    /// <param name="permissions">What the user password is allowed to do.</param>
    /// <param name="encryptMetadata">Whether to encrypt <c>/Metadata</c>.</param>
    /// <param name="algorithm">The cipher to use.</param>
    /// <returns>The encrypted, MAC-protected PDF.</returns>
    public byte[] SaveEncryptedWithMac(
        ReadOnlySpan<byte> userPassword,
        ReadOnlySpan<byte> ownerPassword,
        Permissions permissions,
        bool encryptMetadata,
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes256)
    {
        fixed (byte* user = userPassword)
        fixed (byte* owner = ownerPassword)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_save_encrypted_with_mac(
                    Handle, user, (nuint)userPassword.Length, owner, (nuint)ownerPassword.Length,
                    permissions.Value, Native.FromBool(encryptMetadata), (uint)algorithm, &data, &len),
                "prismpdf_document_save_encrypted_with_mac");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>
    /// Certificate encryption (§7.6.5): any listed recipient's private key opens the result.
    /// </summary>
    /// <param name="recipientCertificates">Recipient certificates, DER-encoded.</param>
    /// <param name="permissions">What recipients are allowed to do.</param>
    /// <param name="encryptMetadata">Whether to encrypt <c>/Metadata</c>.</param>
    /// <param name="algorithm">The cipher to use.</param>
    /// <returns>The encrypted PDF.</returns>
    public byte[] SaveEncryptedForRecipients(
        IReadOnlyList<byte[]> recipientCertificates,
        Permissions permissions,
        bool encryptMetadata,
        EncryptionAlgorithm algorithm = EncryptionAlgorithm.Aes128)
    {
        using var certs = new NativeByteArrays(recipientCertificates, nameof(recipientCertificates));

        byte* data = null;
        nuint len = 0;
        Native.Check(
            NativeMethods.prismpdf_document_save_encrypted_public_key(
                Handle, certs.Pointers, certs.Lengths, certs.Count, permissions.Value,
                Native.FromBool(encryptMetadata), (uint)algorithm, &data, &len),
            "prismpdf_document_save_encrypted_public_key");
        return Native.TakeBytes(data, len);
    }

    /// <summary>
    /// Verify this document's PDF MAC (ISO/TS 32004).
    /// </summary>
    /// <param name="password">The password to open with, encoded as UTF-8.</param>
    /// <returns>
    /// Whether the MAC is valid, or <see langword="null"/> when the document carries none — an
    /// unprotected file, not a failure.
    /// </returns>
    public bool? VerifyPdfMac(ReadOnlySpan<byte> password)
    {
        fixed (byte* pass = password)
        {
            byte valid = 0;
            return Native.CheckOptional(
                NativeMethods.prismpdf_document_verify_pdf_mac(Handle, pass, (nuint)password.Length, &valid),
                "prismpdf_document_verify_pdf_mac")
                ? Native.ToBool(valid)
                : null;
        }
    }

    /// <inheritdoc cref="VerifyPdfMac(ReadOnlySpan{byte})"/>
    public bool? VerifyPdfMac(string password)
    {
        Throw.IfNull(password);
        return VerifyPdfMac(Encoding.UTF8.GetBytes(password));
    }

    // -------------------------------------------------------------------------------------------
    // Signing (§12.8)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Digitally sign, returning an incremental update (§7.5.6) so the original bytes stay intact.
    /// </summary>
    /// <param name="certificateDer">The signer's certificate, DER-encoded.</param>
    /// <param name="privateKeyDer">The matching private key, DER-encoded.</param>
    /// <returns>The signed PDF.</returns>
    public byte[] Sign(ReadOnlySpan<byte> certificateDer, ReadOnlySpan<byte> privateKeyDer)
    {
        fixed (byte* cert = certificateDer)
        fixed (byte* key = privateKeyDer)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_sign(
                    Handle, cert, (nuint)certificateDer.Length, key, (nuint)privateKeyDer.Length,
                    &data, &len),
                "prismpdf_document_sign");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>Digitally sign with explicit settings — reason, location, appearance, PAdES, and more.</summary>
    /// <param name="certificateDer">The signer's certificate, DER-encoded.</param>
    /// <param name="privateKeyDer">The matching private key, DER-encoded.</param>
    /// <param name="settings">The signature's optional inputs.</param>
    /// <returns>The signed PDF.</returns>
    public byte[] Sign(
        ReadOnlySpan<byte> certificateDer, ReadOnlySpan<byte> privateKeyDer, SignSettings settings)
    {
        Throw.IfNull(settings);

        fixed (byte* cert = certificateDer)
        fixed (byte* key = privateKeyDer)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_sign_with(
                    Handle, cert, (nuint)certificateDer.Length, key, (nuint)privateKeyDer.Length,
                    settings.Handle, &data, &len),
                "prismpdf_document_sign_with");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>
    /// Sign an encrypted document and refresh its PDF MAC in the same revision.
    /// </summary>
    /// <param name="certificateDer">The signer's certificate, DER-encoded.</param>
    /// <param name="privateKeyDer">The matching private key, DER-encoded.</param>
    /// <param name="settings">The signature's optional inputs.</param>
    /// <param name="password">The document's password, encoded as UTF-8.</param>
    /// <returns>The signed PDF.</returns>
    public byte[] SignWithMac(
        ReadOnlySpan<byte> certificateDer,
        ReadOnlySpan<byte> privateKeyDer,
        SignSettings settings,
        ReadOnlySpan<byte> password)
    {
        Throw.IfNull(settings);

        fixed (byte* cert = certificateDer)
        fixed (byte* key = privateKeyDer)
        fixed (byte* pass = password)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_sign_with_mac(
                    Handle, cert, (nuint)certificateDer.Length, key, (nuint)privateKeyDer.Length,
                    settings.Handle, pass, (nuint)password.Length, &data, &len),
                "prismpdf_document_sign_with_mac");
            return Native.TakeBytes(data, len);
        }
    }

    /// <summary>
    /// Add a document timestamp (§12.8.5) — proof that the bytes existed at a point in time, with
    /// no signer identity attached.
    /// </summary>
    /// <param name="tsaCertificateDer">The timestamp authority's certificate, DER-encoded.</param>
    /// <param name="tsaPrivateKeyDer">The matching private key, DER-encoded.</param>
    /// <param name="generationTime">
    /// The time to stamp, or <see langword="null"/> to read the current clock. Pin it for a
    /// reproducible build or a test.
    /// </param>
    /// <returns>The timestamped PDF.</returns>
    public byte[] Timestamp(
        ReadOnlySpan<byte> tsaCertificateDer,
        ReadOnlySpan<byte> tsaPrivateKeyDer,
        DateTimeOffset? generationTime = null)
    {
        fixed (byte* cert = tsaCertificateDer)
        fixed (byte* key = tsaPrivateKeyDer)
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(
                NativeMethods.prismpdf_document_timestamp(
                    Handle, cert, (nuint)tsaCertificateDer.Length, key, (nuint)tsaPrivateKeyDer.Length,
                    generationTime is { } time ? (ulong)time.ToUnixTimeSeconds() : 0,
                    Native.FromBool(generationTime.HasValue), &data, &len),
                "prismpdf_document_timestamp");
            return Native.TakeBytes(data, len);
        }
    }

    // -------------------------------------------------------------------------------------------
    // Verification (§12.8, §12.8.4)
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Verify signature <em>integrity</em> only: does each CMS verify, and does it cover what it
    /// claims. Trust is not evaluated, so <see cref="Signature.IsTrusted"/> comes back
    /// <see langword="null"/>.
    /// </summary>
    /// <returns>An owned list; dispose it when done.</returns>
    public SignatureList VerifySignatures()
    {
        nint list = 0;
        Native.Check(NativeMethods.prismpdf_document_verify_signatures(Handle, &list),
            "prismpdf_document_verify_signatures");
        return new SignatureList(list);
    }

    /// <summary>
    /// Verify integrity <em>and</em> trust against the supplied root certificates, which makes
    /// <see cref="Signature.IsTrusted"/> meaningful.
    /// </summary>
    /// <param name="rootCertificates">Trust anchors, DER-encoded.</param>
    /// <returns>An owned list; dispose it when done.</returns>
    public SignatureList VerifySignatures(IReadOnlyList<byte[]> rootCertificates)
    {
        using var roots = new NativeByteArrays(rootCertificates, nameof(rootCertificates));

        nint list = 0;
        Native.Check(
            NativeMethods.prismpdf_document_verify_signatures_with(
                Handle, roots.Pointers, roots.Lengths, roots.Count, &list),
            "prismpdf_document_verify_signatures_with");
        return new SignatureList(list);
    }

    /// <summary>
    /// Verify with long-term validation: trust as above, plus the DSS revocation material, which
    /// makes <see cref="Signature.Revocation"/> meaningful.
    /// </summary>
    /// <param name="rootCertificates">Trust anchors, DER-encoded.</param>
    /// <returns>An owned list; dispose it when done.</returns>
    public SignatureList VerifySignaturesLtv(IReadOnlyList<byte[]> rootCertificates)
    {
        using var roots = new NativeByteArrays(rootCertificates, nameof(rootCertificates));

        nint list = 0;
        Native.Check(
            NativeMethods.prismpdf_document_verify_signatures_ltv(
                Handle, roots.Pointers, roots.Lengths, roots.Count, &list),
            "prismpdf_document_verify_signatures_ltv");
        return new SignatureList(list);
    }
}
