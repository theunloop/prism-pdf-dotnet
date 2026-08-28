using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PrismPdf.Interop;

namespace PrismPdf.Tests;

/// <summary>
/// The raw-layer completeness check — this SDK's analogue of the core repo's
/// <c>crates/pdf-ffi/tests/c/header_surface.c</c>.
/// </summary>
/// <remarks>
/// <para>
/// The binding author's guide requires the raw layer to be "complete for every area the binding
/// ships", and its completeness check to be "the analogue of header_surface.c: every export the
/// binding claims to cover is referenced at least once". In C that means compiling a file that
/// takes the address of every function; in C# the compiler already proves each
/// <c>[DllImport]</c> declaration is well-formed, so the interesting failure is the other
/// direction: <b>a declaration that no longer matches the vendored header</b>.
/// </para>
/// <para>
/// So this fixture parses <c>native/include/prismpdf.h</c> and asserts that every export the raw
/// layer declares still exists in it. It also prints — without failing — which header exports are
/// not bound yet, so the coverage is visible on every run rather than living in a document that
/// can go stale. As of core v0.4.0 nothing is unbound, and this is what says so.
/// </para>
/// </remarks>
[TestFixture]
public sealed class NativeSurfaceTests
{
    private static readonly Regex ExportPattern = new(
        @"\b(prismpdf_[a-z0-9_]+)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Every raw-layer declaration corresponds to an export in the vendored header. A failure here
    /// means the header was re-vendored without regenerating the raw layer.
    /// </summary>
    [Test]
    public void EveryDeclaredExport_ExistsInTheVendoredHeader()
    {
        var header = HeaderExports();
        var declared = DeclaredExports();

        Assert.That(declared, Is.Not.Empty, "the raw layer declares nothing — generation failed?");

        var missing = declared.Where(name => !header.Contains(name)).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.That(missing, Is.Empty,
            "these raw-layer declarations are not in native/include/prismpdf.h. Re-run "
            + "python3 build/gen_native_methods.py after vendoring a new header.");
    }

    /// <summary>
    /// Every declaration is callable: it carries the cdecl calling convention the ABI's
    /// <c>extern "C"</c> functions use, which matters on Windows x86 where the default would not,
    /// and it pins ExactSpelling so the runtime does not probe for A/W variants the ABI lacks.
    /// </summary>
    /// <remarks>
    /// The convention is read off <c>[DllImport]</c> rather than <c>[UnmanagedCallConv]</c>:
    /// this SDK targets netstandard2.0, where the <c>[LibraryImport]</c> source generator that
    /// the latter pairs with does not exist.
    /// </remarks>
    [Test]
    public void EveryDeclaredExport_UsesCdecl()
    {
        var offenders = RawLayerMethods()
            .Where(method => method.GetCustomAttribute<DllImportAttribute>() is not { } import
                || import.CallingConvention != CallingConvention.Cdecl
                || !import.ExactSpelling)
            .Select(method => method.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.That(offenders, Is.Empty, "these declarations do not pin cdecl and ExactSpelling");
    }

    /// <summary>
    /// Reports the coverage of the header by the raw layer. Informational rather than enforced:
    /// the ABI is append-only, so a newly vendored header can add exports this SDK has not bound
    /// yet, and that is a task rather than a defect.
    /// </summary>
    [Test]
    public void CoverageGap_IsReported()
    {
        var header = HeaderExports();
        var declared = DeclaredExports();
        var unbound = header.Except(declared).OrderBy(n => n, StringComparer.Ordinal).ToList();

        TestContext.Out.WriteLine(
            $"Header exports: {header.Count}. Bound by this SDK: {declared.Count}. "
            + $"Not bound yet: {unbound.Count} ({100.0 * declared.Count / header.Count:F1}% covered).");

        foreach (var group in unbound.GroupBy(Area).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            TestContext.Out.WriteLine($"  {group.Key}: {group.Count()}");
        }

        Assert.That(header, Is.Not.Empty, "the vendored header could not be parsed");
    }

    private static HashSet<string> HeaderExports()
    {
        var path = HeaderPath();
        var text = File.ReadAllText(path);

        // Match only declarations, not the doc-comment prose that also names functions: a
        // declaration is a line whose match is followed, eventually, by a `);`.
        var exports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in DeclarationLines(text))
        {
            var match = ExportPattern.Match(line);
            if (match.Success)
            {
                exports.Add(match.Groups[1].Value);
            }
        }

        return exports;
    }

    private static IEnumerable<string> DeclarationLines(string header)
    {
        var buffer = new List<string>();
        var open = false;

        foreach (var raw in header.Split('\n'))
        {
            var line = raw.Trim();

            // Skip comments entirely — they mention function names in prose.
            if (line.StartsWith('*') || line.StartsWith("/*") || line.StartsWith("//"))
            {
                continue;
            }

            if (!open && !ExportPattern.IsMatch(line))
            {
                continue;
            }

            buffer.Add(line);
            open = !line.EndsWith(';');

            if (!open)
            {
                yield return string.Join(' ', buffer);
                buffer.Clear();
            }
        }
    }

    private static HashSet<string> DeclaredExports() =>
        RawLayerMethods().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<MethodInfo> RawLayerMethods() =>
        typeof(NativeMethods)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("prismpdf_", StringComparison.Ordinal));

    private static string Area(string export) => export switch
    {
        _ when export.Contains("composition", StringComparison.Ordinal) => "composition (M25)",
        _ when export.Contains("builder", StringComparison.Ordinal)
            || export.Contains("page_spec", StringComparison.Ordinal)
            || export.Contains("struct_node", StringComparison.Ordinal) => "authoring: builder",
        _ when export.Contains("content_", StringComparison.Ordinal) => "authoring: content streams",
        _ when export.Contains("flow_", StringComparison.Ordinal)
            || export.Contains("table_", StringComparison.Ordinal)
            || export.Contains("text_block", StringComparison.Ordinal)
            || export.Contains("image_source", StringComparison.Ordinal)
            || export.Contains("measure_text", StringComparison.Ordinal)
            || export.Contains("wrap_text", StringComparison.Ordinal) => "layout: flow",
        _ when export.Contains("object_", StringComparison.Ordinal)
            || export.EndsWith("_object", StringComparison.Ordinal)
            || export.Contains("edit_", StringComparison.Ordinal) => "COS inspection and editing",
        _ when export.Contains("pdfa", StringComparison.Ordinal)
            || export.Contains("xmp_metadata", StringComparison.Ordinal) => "conformance: PDF/A, PDF/UA",
        _ => "other",
    };

    private static string HeaderPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "native", "include", "prismpdf.h");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        Assert.Fail("native/include/prismpdf.h was not found above the test output directory.");
        return string.Empty;
    }
}
