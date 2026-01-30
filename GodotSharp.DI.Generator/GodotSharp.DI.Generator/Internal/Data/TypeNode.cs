using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 类型节点
/// </summary>
internal sealed record TypeNode(
    TypeInfo TypeInfo,
    ImmutableArray<DependencyEdge> Dependencies,
    ImmutableArray<ITypeSymbol> ProvidedServices
);
