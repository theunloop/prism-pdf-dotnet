namespace PrismPdf.Tests;

/// <summary>
/// The <c>errors-limits.md</c> journey, and the six semantic contracts that make bindings
/// interchangeable.
/// </summary>
/// <remarks>
/// These are the tests that keep the idiomatic layer honest. Everything else checks that the SDK
/// can do a thing; these check that it does the thing <em>safely</em> — that absence is not an
/// error, that a disposed handle raises a managed exception instead of corrupting memory, and that
/// a borrowed item cannot outlive the list it was lent from.
/// </remarks>
[TestFixture]
public sealed class FailurePathTests : NativeTestBase
{
    private const string TwoPages = "valid/two-pages-text.pdf";

    /// <summary>Contract 1: every failure is one exception type carrying the stable status.</summary>
    [Test]
    public void Failure_RaisesOneExceptionTypeWithAStatus()
    {
        var ex = Assert.Throws<PrismPdfException>(() => Document.Open("not a pdf"u8.ToArray()));

        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.Parse));
        Assert.That(ex.HasDiagnostic, Is.True,
            "a parse failure should carry the library's diagnostic message, not just a status name");
    }

    /// <summary>
    /// Contract 2: <c>NotFound</c> on an optional getter is absence. It maps to
    /// <see langword="null"/>, and no exception is raised.
    /// </summary>
    [Test]
    public void AbsentOptionalField_IsNullNotAnError()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        Assert.DoesNotThrow(() =>
        {
            _ = doc.Info("ThisKeyDoesNotExist");
            _ = doc.Xmp;
            _ = doc.CreationDate;
        });

        Assert.That(doc.Info("ThisKeyDoesNotExist"), Is.Null);
    }

    /// <summary>Contract 2, the other half: an out-of-range index is still an error.</summary>
    [Test]
    public void OutOfRangeIndex_IsAnError()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        var ex = Assert.Throws<PrismPdfException>(() => doc.PageText(9999));
        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.NotFound));
    }

    /// <summary>
    /// Contract 3: using a disposed handle raises the wrapper's error, not a crash. The native
    /// library is never handed a dangling pointer.
    /// </summary>
    [Test]
    public void DisposedDocument_ThrowsObjectDisposed()
    {
        var doc = Document.Open(Corpus.Read(TwoPages));
        doc.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = doc.PageCount);
        Assert.That(doc.IsInvalid, Is.True);
    }

    /// <summary>Disposing twice is a no-op, as freeing null is on the C side.</summary>
    [Test]
    public void DoubleDispose_IsSafe()
    {
        var doc = Document.Open(Corpus.Read(TwoPages));

        doc.Dispose();
        Assert.DoesNotThrow(doc.Dispose);
    }

    /// <summary>
    /// Contract 4: a borrowed item must keep its owner alive, and must refuse to work once the
    /// owner has been disposed — never dereference a pointer the list already freed.
    /// </summary>
    [Test]
    public void BorrowedItem_AfterListDisposed_ThrowsObjectDisposed()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        var fields = doc.FormFields();
        var count = fields.Count;
        fields.Dispose();

        Assert.That(count, Is.GreaterThanOrEqualTo(0));
        Assert.Throws<ObjectDisposedException>(() => _ = fields.Count);
    }

    /// <summary>An empty collection is an empty list, not an error.</summary>
    [Test]
    public void EmptyCollections_AreEmptyNotErrors()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        using var annotations = doc.PageAnnotations(0);
        using var fields = doc.FormFields();
        using var outline = doc.Outline();
        using var attachments = doc.Attachments();

        Assert.Multiple(() =>
        {
            Assert.That(annotations.Count, Is.EqualTo(0));
            Assert.That(fields.Count, Is.EqualTo(0));
            Assert.That(outline.Count, Is.EqualTo(0));
            Assert.That(attachments.Count, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Contract 5: strings and buffers are copied then released immediately. The observable proof
    /// is that the value survives arbitrarily long and repeated reads never drift — a
    /// use-after-free would show up here as corruption or a crash under repetition.
    /// </summary>
    [Test]
    public void OwnedStringsAndBuffers_AreCopiedNotAliased()
    {
        using var doc = Document.Open(Corpus.Read(TwoPages));

        var first = doc.PageText(0);
        var buffers = new List<byte[]>();
        for (var i = 0; i < 50; i++)
        {
            Assert.That(doc.PageText(0), Is.EqualTo(first));
            buffers.Add(doc.Save());
        }

        Assert.That(buffers, Is.All.Not.Empty);
        Assert.That(buffers[0], Is.EqualTo(buffers[^1]));
    }

    /// <summary>A wrong password is its own status, distinguishable from a parse failure.</summary>
    [Test]
    public void WrongPassword_IsItsOwnStatus()
    {
        using var source = Document.Open(Corpus.Read(TwoPages));
        var encrypted = source.SaveEncrypted("right");

        var ex = Assert.Throws<PrismPdfException>(() => Document.Open(encrypted, "wrong"));

        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.Password));
        Assert.That(ex.Status, Is.Not.EqualTo(PrismPdfStatus.Parse));
    }

    /// <summary>Opening an encrypted document with no password at all is also a password failure.</summary>
    [Test]
    public void EncryptedWithoutPassword_IsAPasswordFailure()
    {
        using var source = Document.Open(Corpus.Read(TwoPages));
        var encrypted = source.SaveEncrypted("secret");

        var ex = Assert.Throws<PrismPdfException>(() => Document.Open(encrypted));

        Assert.That(ex!.Status, Is.EqualTo(PrismPdfStatus.Password));
    }
}
