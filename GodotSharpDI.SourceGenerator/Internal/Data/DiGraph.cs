using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// DI dependency graph
/// </summary>
internal sealed record DiGraph(
    ImmutableArray<TypeNode> HostNodes,
    ImmutableArray<TypeNode> UserNodes,
    ImmutableArray<ScopeNode> ScopeNodes,
    ImmutableDictionary<ITypeSymbol, TypeNode> HostNodeMap
);
