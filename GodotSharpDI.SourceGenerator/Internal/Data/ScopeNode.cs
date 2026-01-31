using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Scope 节点
/// </summary>
internal sealed record ScopeNode(
    ValidateTypeInfo ValidateTypeInfo,
    ImmutableArray<INamedTypeSymbol> InstantiateServices,
    ImmutableArray<INamedTypeSymbol> ExpectHosts
);
