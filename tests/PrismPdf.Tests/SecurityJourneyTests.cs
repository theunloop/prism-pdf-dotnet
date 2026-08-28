namespace PrismPdf.Tests;

/// <summary>
/// The security journey: encryption round-trips, permissions, and signature verification.
/// </summary>
[TestFixture]
public sealed class SecurityJourneyTests : NativeTestBase
{
    private const string TwoPages = "valid/two-pages-text.pdf";

    /// <summary>Every algorithm the ABI accepts round-trips through the binding itself.</summary>
    [TestCase(EncryptionAlgorithm.Rc4128)]
    [TestCase(EncryptionAlgorithm.Aes128)]
    [TestCase(EncryptionAlgorithm.Aes256)]
    [TestCase(EncryptionAlgorithm.Aes256Gcm)]
    public void Encrypt_RoundTrips(EncryptionAlgorithm algorithm)
    {
        using var source = Document.Open(Corpus.Read(TwoPages));
        var expectedPages = source.PageCount;

        var encrypted = source.SaveEncrypted("user-password", "owner-password", algorithm);

        using var reopened = Document.Open(encrypted, "user-password");
        Assert.That(reopened.PageCount, Is.EqualTo(expectedPages));
    }

    /// <summary>The owner password opens the document too — the ABI tries the password as both.</summary>
    [Test]
    public void Encrypt_OwnerPasswordAlsoOpens()
    {
        using var source = Document.Open(Corpus.Read(TwoPages));

        var encrypted = source.SaveEncrypted("user-password", "owner-password");

        using var reopened = Document.Open(encrypted, "owner-password");
        Assert.That(reopened.PageCount, Is.EqualTo(source.PageCount));
    }

    /// <summary>
    /// Permissions compose immutably, and granting all eight named operations yields <c>-4</c>, not
    /// <c>All</c>'s <c>-1</c> — <c>ALL</c> also sets reserved bits 1–2, which §7.6.3.2 requires to
    /// be zero.
    /// </summary>
    [Test]
    public void Permissions_ComposeImmutably()
    {
        var restricted = Permissions.Restricted;
        var withPrint = restricted.AllowPrint();

        Assert.That(withPrint, Is.Not.EqualTo(restricted), "each grant returns a widened copy");
        Assert.That(restricted, Is.EqualTo(Permissions.Restricted), "the receiver is untouched");

        var everything = Permissions.Restricted
            .AllowPrint()
            .AllowModify()
            .AllowCopy()
            .AllowAnnotate()
            .AllowFillForms()
            .AllowAccessibility()
            .AllowAssemble()
            .AllowPrintHighRes();

        Assert.That(everything.Value, Is.EqualTo(-4));
        Assert.That(Permissions.All.Value, Is.EqualTo(-1));
    }

    /// <summary>Explicit permissions survive an encrypted round-trip.</summary>
    [Test]
    public void Encrypt_WithExplicitPermissions_RoundTrips()
    {
        using var source = Document.Open(Corpus.Read(TwoPages));
        var permissions = Permissions.Restricted.AllowPrint().AllowAccessibility();

        var encrypted = source.SaveEncrypted(
            "user"u8, "owner"u8, permissions, encryptMetadata: false);

        using var reopened = Document.Open(encrypted, "user");
        Assert.That(reopened.PageCount, Is.EqualTo(source.PageCount));
    }

    /// <summary>An unsigned document reports no signatures — an empty list, not an error.</summary>
    [Test]
    public void VerifySignatures_UnsignedDocument_IsEmpty()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        using var signatures = doc.VerifySignatures();

        Assert.That(signatures.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// A document carrying no PDF MAC reports absence, not failure — an unprotected file is not a
    /// broken one.
    /// </summary>
    [Test]
    public void VerifyPdfMac_WithoutMac_IsNull()
    {
        using var source = Document.Open(Corpus.Read(TwoPages));
        var encrypted = source.SaveEncrypted("password");

        using var reopened = Document.Open(encrypted, "password");

        Assert.That(reopened.VerifyPdfMac("password"), Is.Null);
    }

    /// <summary>
    /// Signing produces an incremental update whose signature verifies for integrity. Trust is a
    /// separate question: with no roots supplied, <c>IsTrusted</c> is null, and this test asserts
    /// that distinction rather than collapsing it.
    /// </summary>
    [Test]
    public void Sign_ThenVerify_ReportsIntegrityButNotTrust()
    {
        var (certificate, key) = TestSigner.LoadOrIgnore();

        using var doc = Document.Open(Corpus.Read(TwoPages));
        using var settings = new SignSettings
        {
            Name = "Prism PDF .NET conformance suite",
            Reason = "Testing",
            SigningTime = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
        };

        var signed = doc.Sign(certificate, key, settings);

        using var reopened = Document.Open(signed);
        using var signatures = reopened.VerifySignatures();

        Assert.That(signatures.Count, Is.EqualTo(1));
        var signature = signatures[0];
        Assert.Multiple(() =>
        {
            Assert.That(signature.IsValid, Is.True, "the CMS should verify");
            Assert.That(signature.IsTrusted, Is.Null, "no roots were supplied, so trust is unevaluated");
            Assert.That(signature.CoveredBytes, Is.GreaterThan(0));
        });
    }

    /// <summary>Signing appends: the original content survives the incremental update.</summary>
    [Test]
    public void Sign_PreservesTheOriginalContent()
    {
        var (certificate, key) = TestSigner.LoadOrIgnore();

        using var doc = Document.Open(Corpus.Read(TwoPages));
        var expectedText = doc.PageText(0);

        using var signed = Document.Open(doc.Sign(certificate, key));

        Assert.That(signed.PageCount, Is.EqualTo(doc.PageCount));
        Assert.That(signed.PageText(0), Is.EqualTo(expectedText));
    }
}

/// <summary>
/// The throwaway signing key the core repo ships for its own tests
/// (<c>crates/pdf/examples/test-signer/</c>). It is a test fixture, not a credential.
/// </summary>
/// <remarks>
/// The one test input the core's release does not package: <c>prism-pdf-corpus-vX.Y.Z.tar.gz</c>
/// carries <c>corpus/{valid,malformed,edge}</c> and nothing else. Rather than make a 400 MB
/// checkout the price of two tests, <c>build/fetch-natives.sh --corpus</c> reads these files out
/// of the pinned tag and stages <c>native/test-signer/</c>; a checkout is still honoured as a
/// second candidate, for anyone who has one for engine work.
/// </remarks>
internal static class TestSigner
{
    private static readonly string[][] Candidates =
    [
        ["native", "test-signer"],
        ["prism-pdf", "crates", "pdf", "examples", "test-signer"],
    ];

    internal static (byte[] Certificate, byte[] Key) LoadOrIgnore()
    {
        var signerDir = Locate();
        if (signerDir is null)
        {
            Assert.Ignore(
                "The core's test signer was not found. Run build/fetch-natives.sh --corpus to "
                + "stage it, which needs an authenticated gh.");
        }

        return (File.ReadAllBytes(Path.Combine(signerDir!, "cert.der")),
                File.ReadAllBytes(Path.Combine(signerDir!, "key.der")));
    }

    private static string? Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var parts in Candidates)
            {
                var candidate = Path.Combine([directory.FullName, .. parts]);
                if (File.Exists(Path.Combine(candidate, "cert.der"))
                    && File.Exists(Path.Combine(candidate, "key.der")))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }
}
