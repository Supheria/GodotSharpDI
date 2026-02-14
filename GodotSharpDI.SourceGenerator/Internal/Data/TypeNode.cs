using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 类型节点
/// </summary>
internal sealed record TypeNode(
    ValidatedTypeInfo ValidatedTypeInfo,
    ImmutableArray<DependencyEdge> Dependencies,
    ImmutableArray<INamedTypeSymbol> ProvidedServices,
    // 服务暴露类型到实现类型的映射
    // Key: 暴露类型 (如接口 IService)
    // Value: 实现类型 (如具体类 ServiceImpl)
    ImmutableDictionary<INamedTypeSymbol, INamedTypeSymbol> ServiceImplementationMap
);
