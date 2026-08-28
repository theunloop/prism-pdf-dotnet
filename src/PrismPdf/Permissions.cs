using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// The access permissions written into an encrypted document's <c>/P</c> flag word (§7.6.3.2).
/// </summary>
/// <remarks>
/// <para>
/// Rule 8 of the binding author's guide: the permission helpers operate on a plain
/// <c>int32</c>, not a handle, so they bind "as an immutable value type with chainable methods".
/// Start from <see cref="Restricted"/> (nothing allowed) or <see cref="All"/> and grant one
/// operation at a time; each method returns a widened copy and leaves the receiver untouched.
/// </para>
/// <para>
/// Granting all eight named operations yields <c>-4</c>, not <see cref="All"/>'s <c>-1</c>:
/// <c>ALL</c> also sets reserved bits 1–2, which §7.6.3.2 requires to be zero. Both are accepted
/// on write; the composed word is the spec-shaped one.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var permissions = Permissions.Restricted.AllowPrint().AllowAccessibility();
/// var bytes = doc.SaveEncrypted("user", "owner", permissions, encryptMetadata: false);
/// </code>
/// </example>
public readonly struct Permissions : IEquatable<Permissions>
{
    private Permissions(int value) => Value = value;

    /// <summary>The raw <c>/P</c> flag word, as the ABI and the PDF file both carry it.</summary>
    public int Value { get; }

    /// <summary>Nothing allowed — the starting point for granting operations one at a time.</summary>
    public static Permissions Restricted => new(NativeMethods.prismpdf_permissions_restricted());

    /// <summary>Everything allowed (<c>-1</c>), reserved bits included.</summary>
    public static Permissions All => new(NativeMethods.prismpdf_permissions_all());

    /// <summary>Grant printing (bit 3).</summary>
    public Permissions AllowPrint() => new(NativeMethods.prismpdf_permissions_allow_print(Value));

    /// <summary>Grant modifying contents (bit 4).</summary>
    public Permissions AllowModify() => new(NativeMethods.prismpdf_permissions_allow_modify(Value));

    /// <summary>Grant copying text and graphics (bit 5).</summary>
    public Permissions AllowCopy() => new(NativeMethods.prismpdf_permissions_allow_copy(Value));

    /// <summary>Grant adding and modifying annotations (bit 6).</summary>
    public Permissions AllowAnnotate() => new(NativeMethods.prismpdf_permissions_allow_annotate(Value));

    /// <summary>Grant filling form fields (bit 9).</summary>
    public Permissions AllowFillForms() => new(NativeMethods.prismpdf_permissions_allow_fill_forms(Value));

    /// <summary>Grant extraction for accessibility (bit 10).</summary>
    public Permissions AllowAccessibility() => new(NativeMethods.prismpdf_permissions_allow_accessibility(Value));

    /// <summary>Grant inserting, rotating and deleting pages (bit 11).</summary>
    public Permissions AllowAssemble() => new(NativeMethods.prismpdf_permissions_allow_assemble(Value));

    /// <summary>Grant full-quality printing (bit 12).</summary>
    public Permissions AllowPrintHighRes() => new(NativeMethods.prismpdf_permissions_allow_print_high_res(Value));

    /// <inheritdoc/>
    public bool Equals(Permissions other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Permissions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => $"Permissions(0x{Value:X8})";

    /// <summary>Compares two permission words.</summary>
    public static bool operator ==(Permissions left, Permissions right) => left.Equals(right);

    /// <summary>Compares two permission words.</summary>
    public static bool operator !=(Permissions left, Permissions right) => !left.Equals(right);
}
