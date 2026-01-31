using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Data;

/// <summary>
/// DI 依赖图
/// </summary>
internal sealed record DiGraph(
    ImmutableArray<TypeNode> ServiceNodes,
    ImmutableArray<TypeNode> HostNodes,
    ImmutableArray<TypeNode> UserNodes,
    ImmutableArray<TypeNode> HostAndUserNodes,
    ImmutableArray<ScopeNode> ScopeNodes,
    ImmutableDictionary<ITypeSymbol, TypeNode> ServiceNodeMap,
    ImmutableDictionary<ITypeSymbol, TypeNode> HostNodeMap,
    ImmutableDictionary<ITypeSymbol, TypeNode> HostAndUserNodeMap
);
