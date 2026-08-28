using System.Runtime.InteropServices;
using PrismPdf.Interop;

namespace PrismPdf.Tests;

/// <summary>
/// The native library loader's probing rules.
/// </summary>
/// <remarks>
/// <para>
/// These matter more than their size suggests: the loader is the one component whose failure mode
/// is <em>the package installs fine and then throws on first use</em>, and it is the component
/// least covered by the rest of the suite, because every other fixture runs against a library the
/// developer staged by hand through <c>PRISMPDF_NATIVE_PATH</c>. What a consumer actually gets is
/// the packaged <c>runtimes/&lt;rid&gt;/native/</c> layout, which nothing else here exercises.
/// </para>
/// <para>
/// Pure managed, like <c>CompatTests</c>: no native library, no corpus, no Rust toolchain.
/// </para>
/// </remarks>
[TestFixture]
public sealed class LoaderTests
{
    /// <summary>The library file name follows the platform's own convention.</summary>
    [Test]
    public void FileName_FollowsThePlatformConvention()
    {
        var fileName = NativeLibraryResolver.FileName();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.That(fileName, Is.EqualTo("pdf_ffi.dll"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.That(fileName, Is.EqualTo("libpdf_ffi.dylib"));
        }
        else
        {
            Assert.That(fileName, Is.EqualTo("libpdf_ffi.so"));
        }
    }

    /// <summary>
    /// The computed identifiers name this host, and the last one is always the plain
    /// (non-musl) form so a package shipping only that build is still reachable.
    /// </summary>
    [Test]
    public void RuntimeIdentifiers_NameThisHost()
    {
        var rids = NativeLibraryResolver.RuntimeIdentifiers().ToList();

        Assert.That(rids, Is.Not.Empty, "every architecture the SDK ships must produce an identifier");
        Assert.That(rids, Is.Unique);

        var prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx-"
            : "linux-";

        Assert.That(rids, Is.All.StartWith(prefix));
        Assert.That(rids[^1], Does.Not.Contain("musl"),
            "the plain identifier is the last resort, so it must come last");
    }

    /// <summary>
    /// The architecture segment is the <em>process</em>'s, not the OS's. An x86 process on x64
    /// Windows, or an emulated x64 process on Windows ARM64, needs the build matching the process.
    /// </summary>
    [Test]
    public void RuntimeIdentifiers_UseTheProcessArchitecture()
    {
        var expected = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

        if (expected is null)
        {
            Assert.Ignore($"The SDK ships no build for {RuntimeInformation.ProcessArchitecture}.");
        }

        Assert.That(NativeLibraryResolver.RuntimeIdentifiers(), Is.All.EndWith($"-{expected}"));
    }

    /// <summary>
    /// The packaged layout is probed directly. The default probing rules read it out of the
    /// application's <c>deps.json</c>, so a host without one — .NET Framework, Mono — would
    /// otherwise never look in the folder the NuGet package actually ships.
    /// </summary>
    [Test]
    public void CandidatePaths_IncludeThePackagedRuntimesLayout()
    {
        var expected = NativeLibraryResolver.RuntimeIdentifiers()
            .Select(rid => Path.Combine(
                AppContext.BaseDirectory, "runtimes", rid, "native", NativeLibraryResolver.FileName()));

        Assert.That(NativeLibraryResolver.CandidatePaths(), Is.SupersetOf(expected));
    }

    /// <summary>The developer's override wins over anything shipped beside the assembly.</summary>
    [Test]
    public void CandidatePaths_PreferTheConfiguredOverride()
    {
        var original = Environment.GetEnvironmentVariable(NativeLibraryResolver.PathVariable);
        try
        {
            var configured = Path.Combine(Path.GetTempPath(), "prismpdf-loader-test");
            Environment.SetEnvironmentVariable(NativeLibraryResolver.PathVariable, configured);

            var candidates = NativeLibraryResolver.CandidatePaths().ToList();

            Assert.That(candidates[0], Is.EqualTo(configured), "the directory itself, for a full-path override");
            Assert.That(candidates[1], Is.EqualTo(Path.Combine(configured, NativeLibraryResolver.FileName())));
            Assert.That(candidates.Skip(2), Is.All.StartWith(AppContext.BaseDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NativeLibraryResolver.PathVariable, original);
        }
    }

    /// <summary>
    /// Every probed path is one the loader can actually attempt: rooted, and named for this
    /// platform. A relative candidate would resolve against the working directory, which is not
    /// something a library gets to assume.
    /// </summary>
    [Test]
    public void CandidatePaths_AreRootedAndPlatformNamed()
    {
        var original = Environment.GetEnvironmentVariable(NativeLibraryResolver.PathVariable);
        try
        {
            Environment.SetEnvironmentVariable(NativeLibraryResolver.PathVariable, null);

            var candidates = NativeLibraryResolver.CandidatePaths().ToList();

            Assert.That(candidates, Is.Not.Empty);
            Assert.That(candidates, Is.All.Matches<string>(Path.IsPathRooted));
            Assert.That(candidates, Is.All.EndWith(NativeLibraryResolver.FileName()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NativeLibraryResolver.PathVariable, original);
        }
    }
}
