using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Data;

/// <summary>
/// Scope 节点
/// </summary>
internal sealed record ScopeNode(
    TypeInfo TypeInfo,
    ImmutableArray<INamedTypeSymbol> InstantiateServices,
    ImmutableArray<INamedTypeSymbol> ExpectHosts
);
