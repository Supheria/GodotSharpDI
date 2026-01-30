using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// Scope 节点
/// </summary>
internal sealed record ScopeNode(
    TypeInfo TypeInfo,
    ImmutableArray<ITypeSymbol> InstantiateServices,
    ImmutableArray<ITypeSymbol> ExpectHosts,
    ImmutableArray<ITypeSymbol> AllProvidedServices
);
