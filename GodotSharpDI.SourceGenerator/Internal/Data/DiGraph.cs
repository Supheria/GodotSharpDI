using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// DI 依赖图
/// </summary>
internal sealed record DiGraph(
    ImmutableArray<TypeNode> ServiceNodes,    // Service 和 Provider
    ImmutableArray<TypeNode> HostNodes,
    ImmutableArray<TypeNode> UserNodes,
    ImmutableArray<ScopeNode> ScopeNodes,
    ImmutableDictionary<ITypeSymbol, TypeNode> ServiceNodeMap,
    ImmutableDictionary<ITypeSymbol, TypeNode> HostNodeMap
);
