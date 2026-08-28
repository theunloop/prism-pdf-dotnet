using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>The results of verifying a document's digital signatures (§12.8, §12.8.4).</summary>
public sealed unsafe class SignatureList : NativeList<Signature>
{
    internal SignatureList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_signature_list_len(Handle, &len), "prismpdf_signature_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override Signature this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_signature_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_signature_list_get");
            return new Signature(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_signature_list_free(handle);
}

/// <summary>One verified signature, borrowed from its <see cref="SignatureList"/>.</summary>
public sealed unsafe class Signature : BorrowedItem
{
    internal Signature(SignatureList owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary>
    /// Whether the CMS verifies and covers what it claims — <em>integrity, not trust</em>. A
    /// self-signed certificate can be perfectly valid here.
    /// </summary>
    public bool IsValid
    {
        get
        {
            byte valid = 0;
            Native.Check(NativeMethods.prismpdf_signature_valid(Item, &valid), "prismpdf_signature_valid");
            return Native.ToBool(valid);
        }
    }

    /// <summary>The signer's distinguished name, or <see langword="null"/> when unavailable.</summary>
    public string? Signer
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_signature_signer(Item, &text),
                "prismpdf_signature_signer")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <summary>
    /// How many bytes of the file the signature covers. Compare against the file length to detect
    /// content appended after signing.
    /// </summary>
    public long CoveredBytes
    {
        get
        {
            nuint bytes = 0;
            Native.Check(NativeMethods.prismpdf_signature_covered_bytes(Item, &bytes),
                "prismpdf_signature_covered_bytes");
            return (long)bytes;
        }
    }

    /// <summary>The claimed signing time, or <see langword="null"/> when the signature claims none.</summary>
    public DateTimeOffset? SigningTime
    {
        get
        {
            long time = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_signature_signing_time(Item, &time),
                "prismpdf_signature_signing_time")
                ? DateTimeOffset.FromUnixTimeSeconds(time)
                : null;
        }
    }

    /// <summary>
    /// The time asserted by an embedded timestamp token, or <see langword="null"/> when there is
    /// none. Unlike <see cref="SigningTime"/>, this one is attested by a third party.
    /// </summary>
    public DateTimeOffset? TimestampTime
    {
        get
        {
            long time = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_signature_timestamp_time(Item, &time),
                "prismpdf_signature_timestamp_time")
                ? DateTimeOffset.FromUnixTimeSeconds(time)
                : null;
        }
    }

    /// <summary>
    /// Whether the chain is trusted: <see langword="null"/> means trust was never evaluated (no
    /// roots were supplied), <see langword="false"/> means it was evaluated and the chain is
    /// <em>not</em> trusted. The distinction matters — do not collapse it to a boolean.
    /// </summary>
    public bool? IsTrusted
    {
        get
        {
            byte trusted = 0;
            return Native.CheckOptional(NativeMethods.prismpdf_signature_trusted(Item, &trusted),
                "prismpdf_signature_trusted")
                ? Native.ToBool(trusted)
                : null;
        }
    }

    /// <summary>Whether this is a PAdES (ETSI EN 319 142) signature.</summary>
    public bool IsPades
    {
        get
        {
            byte pades = 0;
            Native.Check(NativeMethods.prismpdf_signature_pades(Item, &pades), "prismpdf_signature_pades");
            return Native.ToBool(pades);
        }
    }

    /// <summary>
    /// The chain's revocation state, or <see langword="null"/> when revocation was not evaluated or
    /// no chain could be built.
    /// </summary>
    public Revocation? Revocation
    {
        get
        {
            Revocation revocation = default;
            return Native.CheckOptional(NativeMethods.prismpdf_signature_revocation(Item, &revocation),
                "prismpdf_signature_revocation")
                ? revocation
                : null;
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var trust = IsTrusted switch
        {
            null => "trust not evaluated",
            true => "trusted",
            false => "NOT trusted",
        };
        return $"{Signer ?? "<unknown signer>"}: {(IsValid ? "valid" : "INVALID")}, {trust}";
    }
}
