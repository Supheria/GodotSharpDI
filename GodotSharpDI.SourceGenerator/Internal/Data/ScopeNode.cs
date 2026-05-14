using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Scope node
/// </summary>
internal sealed record ScopeNode(
    ValidatedTypeInfo ValidatedTypeInfo,
    ImmutableArray<INamedTypeSymbol> ExpectHosts
);
