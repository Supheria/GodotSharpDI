using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Raw class semantic information (Raw)
/// </summary>
internal sealed record RawClassSemanticInfo(
    INamedTypeSymbol Symbol,
    Location Location,
    bool HasHostAttribute,
    bool HasUserAttribute,
    bool HasModulesAttribute,
    bool ImplementsIScope,
    bool ImplementsIDependenciesResolved,
    bool IsNode,
    bool IsPartial,
    ImmutableArray<ISymbol> Members
);
