using System.Reflection;
using System.Runtime.InteropServices;

namespace PrismPdf.Interop;

/// <summary>
/// Teaches the loader where to find <c>pdf_ffi</c>.
/// </summary>
/// <remarks>
/// <para>
/// The default probing rules already find the library when it sits beside the assembly or in a
/// <c>runtimes/&lt;rid&gt;/native/</c> folder, which is how the NuGet package ships it. This
/// resolver adds the cases those rules do not cover:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>PRISMPDF_NATIVE_PATH</c> — a directory, or the full path to the library file. Set it to
///     <c>native/lib</c> after <c>build/build-native.sh</c> and everything works against a local
///     core checkout.
///   </description></item>
///   <item><description>
///     <c>runtimes/&lt;rid&gt;/native/</c> next to the assembly. The default rules read this from
///     the application's <c>deps.json</c>, which means they find it only on a runtime that has
///     one — so a .NET Framework or Mono host, or anyone who simply copied the package layout out
///     of a NuGet cache, would otherwise miss the very folder the package ships. Probing it
///     directly is what makes one package serve every host.
///   </description></item>
///   <item><description>
///     <c>native/lib/</c> next to the assembly, where the build script copies its output.
///   </description></item>
/// </list>
/// <para>
/// <b>Why this is not a plain <c>NativeLibrary.SetDllImportResolver</c> call.</b> This SDK targets
/// netstandard2.0, and <c>NativeLibrary</c> is .NET Core 3.0+. So the resolver is installed
/// reflectively when the hosting runtime does have it — which is every .NET Core, .NET 5+ and
/// .NET 8+ host, and gives the full probing order below — and falls back to pre-loading the
/// library through the platform loader when it does not.
/// </para>
/// <para>
/// The fallback is weaker, and deliberately so rather than silently: pre-loading works on .NET
/// Framework, where <c>LoadLibraryW</c> of a full path makes the later <c>[DllImport("pdf_ffi")]</c>
/// bind to the module already in the process. On Mono under Unix the runtime may still resolve the
/// bare name through its own search path instead, so a consumer there should either place the
/// library beside the application or use the platform's loader variables
/// (<c>LD_LIBRARY_PATH</c>, <c>DYLD_LIBRARY_PATH</c>). See <c>docs/native-build.md</c>.
/// </para>
/// </remarks>
internal static class NativeLibraryResolver
{
    /// <summary>The environment variable that overrides where the native library is loaded from.</summary>
    internal const string PathVariable = "PRISMPDF_NATIVE_PATH";

    private static int _registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
        {
            return;
        }

        if (TryInstallRuntimeResolver())
        {
            return;
        }

        PreloadFirstCandidate();
    }

    /// <summary>
    /// Install <see cref="Resolve"/> through <c>NativeLibrary.SetDllImportResolver</c> if the
    /// hosting runtime has it. Returns whether the hook is in place.
    /// </summary>
    private static bool TryInstallRuntimeResolver()
    {
        try
        {
            var nativeLibrary = FindType("System.Runtime.InteropServices.NativeLibrary");
            var resolverDelegate = FindType("System.Runtime.InteropServices.DllImportResolver");
            if (nativeLibrary is null || resolverDelegate is null)
            {
                return false;
            }

            var setter = nativeLibrary.GetMethod(
                "SetDllImportResolver", BindingFlags.Public | BindingFlags.Static);
            var resolve = typeof(NativeLibraryResolver).GetMethod(
                nameof(Resolve), BindingFlags.NonPublic | BindingFlags.Static);
            if (setter is null || resolve is null)
            {
                return false;
            }

            var handler = Delegate.CreateDelegate(resolverDelegate, resolve, throwOnBindFailure: false);
            if (handler is null)
            {
                return false;
            }

            setter.Invoke(null, new object[] { typeof(NativeLibraryResolver).Assembly, handler });
            return true;
        }
        catch (Exception ex) when (ex is MemberAccessException or TargetInvocationException
            or NotSupportedException or PlatformNotSupportedException)
        {
            // A runtime that has the API but will not let us call it reflectively. The pre-load
            // fallback still gives PRISMPDF_NATIVE_PATH a chance to work.
            return false;
        }
    }

    private static Type? FindType(string fullName) =>
        Type.GetType(fullName, throwOnError: false)
        ?? Type.GetType($"{fullName}, System.Runtime.InteropServices", throwOnError: false)
        ?? Type.GetType($"{fullName}, System.Runtime", throwOnError: false);

    /// <summary>
    /// The <c>DllImportResolver</c> body. Bound by name, so its signature must stay exactly the
    /// delegate's: <c>IntPtr (string, Assembly, DllImportSearchPath?)</c>.
    /// </summary>
    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, NativeMethods.Library, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in CandidatePaths())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var handle = PlatformLoad(candidate);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }
        }

        // Fall through to the default probing rules, which cover the packaged runtimes/ layout.
        return IntPtr.Zero;
    }

    /// <summary>
    /// Load the first candidate that exists, so the module is already in the process by the time
    /// the first P/Invoke binds. The fallback for runtimes without <c>NativeLibrary</c>.
    /// </summary>
    private static void PreloadFirstCandidate()
    {
        foreach (var candidate in CandidatePaths())
        {
            if (File.Exists(candidate) && PlatformLoad(candidate) != IntPtr.Zero)
            {
                return;
            }
        }
    }

    internal static IEnumerable<string> CandidatePaths()
    {
        var fileName = FileName();

        var configured = Environment.GetEnvironmentVariable(PathVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            // Accept either the directory holding the library or the library file itself.
            yield return configured!;
            yield return Path.Combine(configured!, fileName);
        }

        // AppContext.BaseDirectory rather than Assembly.Location: the latter is empty in a
        // single-file app, and this resolver must keep working when the SDK is bundled into one.
        var baseDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDirectory))
        {
            yield break;
        }

        yield return Path.Combine(baseDirectory, fileName);

        foreach (var rid in RuntimeIdentifiers())
        {
            yield return Path.Combine(baseDirectory, "runtimes", rid, "native", fileName);
        }

        yield return Path.Combine(baseDirectory, "native", "lib", fileName);
    }

    /// <summary>
    /// The NuGet runtime identifiers this process can load, most specific first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Process architecture, not OS architecture.</b> What matters is the bitness of the
    /// process the library is loading into: an x86 process on an x64 Windows install needs the x86
    /// build, and an x64 process emulated on Windows ARM64 needs the x64 one. Asking about the OS
    /// would get both of those wrong.
    /// </para>
    /// <para>
    /// On Linux the libc flavour is part of the identifier, because a glibc build genuinely cannot
    /// load on musl. netstandard2.0 exposes no API for this, so it is detected by looking for
    /// musl's loader. A musl system yields the musl identifier first and the plain one after, so a
    /// package that ships only the glibc build still gets a chance rather than being ruled out by
    /// this resolver — the loader will refuse it, which is the correct place for that to fail.
    /// </para>
    /// </remarks>
    internal static IEnumerable<string> RuntimeIdentifiers()
    {
        var architecture = Architecture();
        if (architecture is null)
        {
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return $"win-{architecture}";
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return $"osx-{architecture}";
            yield break;
        }

        if (IsMusl())
        {
            yield return $"linux-musl-{architecture}";
        }

        yield return $"linux-{architecture}";
    }

    /// <summary>The RID architecture segment, or <see langword="null"/> on an architecture the SDK does not ship.</summary>
    private static string? Architecture() => RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.X86 => "x86",
        System.Runtime.InteropServices.Architecture.X64 => "x64",
        System.Runtime.InteropServices.Architecture.Arm => "arm",
        System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
        _ => null,
    };

    /// <summary>
    /// Whether this is a musl system — Alpine and friends. Detected by musl's loader, which is
    /// installed as <c>/lib/ld-musl-&lt;arch&gt;.so.1</c>, because netstandard2.0 has no API that
    /// reports the libc flavour.
    /// </summary>
    private static bool IsMusl()
    {
        try
        {
            return Directory.Exists("/lib")
                && Directory.EnumerateFiles("/lib", "ld-musl-*.so.1").Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable /lib tells us nothing; assume the common case.
            return false;
        }
    }

    internal static string FileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"{NativeMethods.Library}.dll";
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? $"lib{NativeMethods.Library}.dylib"
            : $"lib{NativeMethods.Library}.so";
    }

    private static IntPtr PlatformLoad(string path)
    {
        try
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Windows.LoadLibraryW(path)
                : Unix.Load(path, RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return IntPtr.Zero;
        }
    }

    private static class Windows
    {
        [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode,
            SetLastError = true, ExactSpelling = true)]
        internal static extern IntPtr LoadLibraryW(string lpLibFileName);
    }

    private static class Unix
    {
        private const int RtldNow = 0x2;

        // RTLD_GLOBAL is one of the few dlfcn constants that differ between the two platforms.
        private const int RtldGlobalLinux = 0x100;
        private const int RtldGlobalMacOs = 0x8;

        internal static IntPtr Load(string path, bool macOs)
        {
            if (macOs)
            {
                return TryEach(path, RtldNow | RtldGlobalMacOs, DlopenSystem, DlopenLegacy);
            }

            // libdl.so.2 is glibc's; the bare name covers older glibc layouts. musl has neither —
            // it implements dlopen inside libc itself — so libc is tried last rather than being a
            // case that silently fails and takes PRISMPDF_NATIVE_PATH down with it on Alpine.
            return TryEach(path, RtldNow | RtldGlobalLinux, DlopenGlibc, DlopenLegacy, DlopenLibc);
        }

        private static IntPtr TryEach(string path, int mode, params Func<string, int, IntPtr>[] loaders)
        {
            foreach (var loader in loaders)
            {
                try
                {
                    return loader(path, mode);
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
                {
                    // That soname is not present on this distribution; try the next.
                }
            }

            return IntPtr.Zero;
        }

        [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr DlopenGlibc(string file, int mode);

        [DllImport("libdl", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr DlopenLegacy(string file, int mode);

        // musl: dlopen lives in libc, and there is no libdl to find.
        [DllImport("libc", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr DlopenLibc(string file, int mode);

        [DllImport("libSystem.dylib", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr DlopenSystem(string file, int mode);
    }
}
