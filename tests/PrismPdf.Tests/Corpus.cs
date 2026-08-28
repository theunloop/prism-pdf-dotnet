namespace PrismPdf.Tests;

/// <summary>
/// Locates the shared test corpus.
/// </summary>
/// <remarks>
/// <para>
/// The binding author's guide asks every binding's conformance suite to run "against the same
/// inputs, asserting the same facts" as every other binding — specifically the files in the core
/// repo's <c>corpus/{valid,malformed,edge}</c>. Rather than fork those bytes, this SDK reads them
/// out of a local <c>prism-pdf</c> checkout: forking them would let the two drift, and the
/// point of the shared corpus is that they cannot.
/// </para>
/// <para>
/// Set <c>PRISMPDF_CORPUS</c> to override the location; otherwise the checkout is looked for beside
/// the repository root.
/// </para>
/// </remarks>
internal static class Corpus
{
    internal const string PathVariable = "PRISMPDF_CORPUS";

    /// <summary>The corpus root, or <see langword="null"/> when no checkout can be found.</summary>
    internal static string? Root { get; } = Locate();

    /// <summary>Whether the corpus is available. Suites skip rather than fail when it is not.</summary>
    internal static bool IsAvailable => Root is not null;

    /// <summary>Skip the current test when there is no corpus checkout to read.</summary>
    internal static void RequireCorpus()
    {
        if (!IsAvailable)
        {
            Assert.Ignore(
                $"No Prism PDF corpus found. Clone prism-pdf beside this repo, or set {PathVariable}.");
        }
    }

    /// <summary>Read one corpus file.</summary>
    /// <param name="relativePath">Path below the corpus root, e.g. <c>valid/two-pages-text.pdf</c>.</param>
    internal static byte[] Read(string relativePath)
    {
        RequireCorpus();
        return File.ReadAllBytes(Path.Combine(Root!, relativePath));
    }

    /// <summary>Every <c>*.pdf</c> in one corpus directory, sorted for a stable test order.</summary>
    /// <param name="directory">A corpus subdirectory, e.g. <c>valid</c>.</param>
    internal static IEnumerable<string> Files(string directory)
    {
        var root = Locate();
        if (root is null)
        {
            yield break;
        }

        var path = Path.Combine(root, directory);
        if (!Directory.Exists(path))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.pdf").OrderBy(f => f, StringComparer.Ordinal))
        {
            yield return file;
        }
    }

    private static string? Locate()
    {
        var configured = Environment.GetEnvironmentVariable(PathVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        // `native/corpus` is what build/fetch-natives.sh --corpus stages from the published
        // artifact, and is the normal case. `prism-pdf/corpus` is a checkout of the core beside
        // this repo, kept as a second candidate for anyone who has one for engine work.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, "native", "corpus"),
                         Path.Combine(directory.FullName, "prism-pdf", "corpus"),
                     })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }
}

/// <summary>
/// Base fixture for suites that call into the native library.
/// </summary>
/// <remarks>
/// Inheriting this turns "pdf_ffi is not built yet" into one clear skip per fixture, instead of a
/// wall of <see cref="DllNotFoundException"/>. It is deliberately opt-in rather than a namespace-wide
/// <c>SetUpFixture</c>: the header-parity checks in <c>NativeSurfaceTests</c> are pure managed code
/// and must keep running on a machine that has no Rust toolchain at all.
/// </remarks>
public abstract class NativeTestBase
{
    /// <summary>Prove the native library loads before running the fixture's tests.</summary>
    [OneTimeSetUp]
    public void EnsureNativeLibrary()
    {
        try
        {
            _ = Pdf.Version;
        }
        catch (DllNotFoundException ex)
        {
            Assert.Ignore(
                $"The native library pdf_ffi could not be loaded ({ex.Message}). "
                + "Run build/build-native.sh and set PRISMPDF_NATIVE_PATH. See docs/native-build.md.");
        }
    }
}
