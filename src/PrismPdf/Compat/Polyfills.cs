// Types the C# compiler requires by name but netstandard2.0 does not ship.
//
// All three are pure compiler plumbing: the compiler matches them by full name and never needs a
// reference to the runtime's own copy. Declaring them internally is the documented way to use the
// corresponding language features on netstandard2.0, and keeps them off this SDK's public surface.

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Marks the modreq the compiler emits on an <c>init</c> accessor. Required by the
/// <c>readonly record struct</c>s in the idiomatic layer and by
/// <c>PrismPdfException.HasDiagnostic</c>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class IsExternalInit
{
}

/// <summary>
/// Marks the method the runtime runs at module load. The raw layer uses it to install the native
/// library loader before any P/Invoke can be called.
/// </summary>
/// <remarks>
/// Module initializers are a CLI feature rather than a Core-only one — the compiler emits a
/// <c>.cctor</c> on <c>&lt;Module&gt;</c>, which every CLI runtime already runs — so this keeps
/// working on .NET Framework, where the attribute itself does not exist.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class ModuleInitializerAttribute : Attribute
{
}

/// <summary>
/// Lets the argument guards in <c>Throw</c> capture the caller's expression text, so they report
/// the same parameter names the BCL's own <c>ThrowIf*</c> helpers would.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;

    public string ParameterName { get; }
}
