using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 原始类语义信息（Raw）
/// </summary>
internal sealed record RawClassSemanticInfo(
    INamedTypeSymbol Symbol,
    Location Location,
    bool HasSingletonAttribute,    // 保留用于向后兼容
    bool HasProviderAttribute,     // 新增：[Provider] 特性
    bool HasHostAttribute,
    bool HasUserAttribute,
    bool HasModulesAttribute,
    bool ImplementsIScope,
    bool ImplementsIDependenciesResolved,
    bool IsNode,
    bool IsPartial,
    ImmutableArray<ISymbol> Members,
    ImmutableArray<IMethodSymbol> Constructors
);
