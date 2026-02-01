using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Scope 节点
/// </summary>
internal sealed record ScopeNode(
    ValidatedTypeInfo ValidatedTypeInfo,
    ImmutableArray<INamedTypeSymbol> InstantiateServices,
    ImmutableArray<INamedTypeSymbol> ExpectHosts
);
