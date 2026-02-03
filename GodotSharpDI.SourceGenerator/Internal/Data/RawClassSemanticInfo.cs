using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

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
