using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 类型节点
/// </summary>
internal sealed record TypeNode(
    ValidatedTypeInfo ValidatedTypeInfo,
    ImmutableArray<DependencyEdge> Dependencies,
    ImmutableArray<ITypeSymbol> ProvidedServices
);
