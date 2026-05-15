using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Dependency edge - Contains source member information to support more precise circular dependency detection
/// </summary>
internal sealed record DependencyEdge(
    ITypeSymbol TargetType,
    Location Location,
    DependencySource Source,
    // New: Source member name (for precise tracking of WaitFor dependencies)
    string? SourceMemberName = null,
    // New: Service type provided by the source member (for WaitFor scenarios)
    ITypeSymbol? SourceProvidedType = null
);
