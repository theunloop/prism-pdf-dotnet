using System.Runtime.CompilerServices;

namespace PrismPdf;

/// <summary>
/// The argument guards this SDK uses, in the shape netstandard2.0 lacks.
/// </summary>
/// <remarks>
/// .NET 6 and 7 added <c>ArgumentNullException.ThrowIfNull</c> and friends; netstandard2.0 has
/// only the constructors. These helpers throw exactly what the BCL versions throw, including the
/// parameter name, so the exceptions a caller sees do not depend on which runtime is hosting the
/// binding.
/// </remarks>
internal static class Throw
{
    /// <summary>Throw <see cref="ArgumentNullException"/> when <paramref name="argument"/> is null.</summary>
    internal static void IfNull(
        object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    /// <summary>Throw <see cref="ArgumentOutOfRangeException"/> when <paramref name="value"/> is negative.</summary>
    internal static void IfNegative(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, $"{paramName} ('{value}') must be a non-negative value.");
        }
    }

    /// <summary>Throw <see cref="ArgumentOutOfRangeException"/> when <paramref name="value"/> exceeds <paramref name="other"/>.</summary>
    internal static void IfGreaterThan(
        int value,
        int other,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value > other)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, $"{paramName} ('{value}') must be less than or equal to '{other}'.");
        }
    }

    /// <summary>Throw <see cref="ObjectDisposedException"/> when <paramref name="condition"/> holds.</summary>
    internal static void IfDisposed(bool condition, object instance)
    {
        if (condition)
        {
            throw new ObjectDisposedException(instance?.GetType().FullName);
        }
    }
}
