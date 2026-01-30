using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 类型角色
/// </summary>
internal enum TypeRole
{
    None,
    Service, // 纯服务（Singleton/Transient）
    Host, // 仅 Host
    User, // 仅 User
    HostAndUser, // Host + User
    Scope, // Scope
}

/// <summary>
/// 原始类语义信息（Raw）
/// </summary>
internal sealed record RawClassSemanticInfo(
    INamedTypeSymbol Symbol,
    Location Location,
    bool HasSingletonAttribute,
    bool HasHostAttribute,
    bool HasUserAttribute,
    bool HasModulesAttribute,
    bool ImplementsIScope,
    bool ImplementsIServicesReady,
    bool IsNode,
    bool IsPartial,
    ImmutableArray<ISymbol> Members,
    ImmutableArray<IMethodSymbol> Constructors
);

/// <summary>
/// 类验证结果
/// </summary>
internal sealed record ClassValidationResult(
    TypeInfo? TypeInfo,
    ImmutableArray<Diagnostic> Diagnostics
);

/// <summary>
/// 类型信息（验证后）
/// </summary>
internal sealed record TypeInfo(
    INamedTypeSymbol Symbol,
    Location Location,
    TypeRole Role,
    bool ImplementsIServicesReady,
    bool IsNode,
    ImmutableArray<MemberInfo> Members,
    ConstructorInfo? Constructor,
    ModulesInfo? ModulesInfo
);

/// <summary>
/// 成员信息
/// </summary>
internal sealed record MemberInfo(
    ISymbol Symbol,
    Location Location,
    MemberKind Kind,
    ITypeSymbol MemberType,
    ImmutableArray<ITypeSymbol> ExposedTypes
);

internal enum MemberKind
{
    None,
    InjectField,
    InjectProperty,
    SingletonField,
    SingletonProperty,
}

/// <summary>
/// 构造函数信息
/// </summary>
internal sealed record ConstructorInfo(
    IMethodSymbol Symbol,
    Location Location,
    ImmutableArray<ParameterInfo> Parameters
);

/// <summary>
/// 参数信息
/// </summary>
internal sealed record ParameterInfo(IParameterSymbol Symbol, Location Location, ITypeSymbol Type);

/// <summary>
/// 模块信息
/// </summary>
internal sealed record ModulesInfo(
    ImmutableArray<ITypeSymbol> Services,
    ImmutableArray<ITypeSymbol> Hosts
);

/// <summary>
/// DI 图构建结果
/// </summary>
internal sealed record DiGraphBuildResult(DiGraph? Graph, ImmutableArray<Diagnostic> Diagnostics)
{
    public static DiGraphBuildResult Empty => new(null, ImmutableArray<Diagnostic>.Empty);
}

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

/// <summary>
/// 类型节点
/// </summary>
internal sealed record TypeNode(
    TypeInfo TypeInfo,
    ImmutableArray<DependencyEdge> Dependencies,
    ImmutableArray<ITypeSymbol> ProvidedServices
);

/// <summary>
/// Scope 节点
/// </summary>
internal sealed record ScopeNode(
    TypeInfo TypeInfo,
    ImmutableArray<ITypeSymbol> InstantiateServices,
    ImmutableArray<ITypeSymbol> ExpectHosts,
    ImmutableArray<ITypeSymbol> AllProvidedServices
);

/// <summary>
/// 依赖边
/// </summary>
internal sealed record DependencyEdge(
    ITypeSymbol TargetType,
    Location Location,
    DependencySource Source
);

internal enum DependencySource
{
    Constructor,
    InjectMember,
}
