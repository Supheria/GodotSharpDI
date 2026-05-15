using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// Diagnostic builder - Unified diagnostic creation interface
/// </summary>
internal static class DiagnosticBuilder
{
    /// <summary>
    /// Create diagnostic
    /// </summary>
    public static Diagnostic Create(
        DiagnosticDescriptor descriptor,
        Location location,
        params object[] messageArgs
    )
    {
        return Diagnostic.Create(descriptor, location, messageArgs);
    }

    /// <summary>
    /// Create diagnostic (using default location)
    /// </summary>
    public static Diagnostic CreateAtNone(
        DiagnosticDescriptor descriptor,
        params object[] messageArgs
    )
    {
        return Diagnostic.Create(descriptor, Location.None, messageArgs);
    }

    /// <summary>
    /// Create diagnostic for a symbol
    /// </summary>
    public static Diagnostic CreateForSymbol(
        DiagnosticDescriptor descriptor,
        ISymbol symbol,
        params object[] messageArgs
    )
    {
        var location = symbol.Locations.FirstOrDefault() ?? Location.None;
        return Diagnostic.Create(descriptor, location, messageArgs);
    }

    /// <summary>
    /// Create multiple diagnostics in batch
    /// </summary>
    public static IEnumerable<Diagnostic> CreateMultiple(
        DiagnosticDescriptor descriptor,
        IEnumerable<(Location Location, object[] Args)> items
    )
    {
        foreach (var (location, args) in items)
        {
            yield return Diagnostic.Create(descriptor, location, args);
        }
    }
}
