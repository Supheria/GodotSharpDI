using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 模块信息
/// </summary>
internal sealed record ModulesInfo(
    ImmutableArray<INamedTypeSymbol> Services,
    ImmutableArray<INamedTypeSymbol> Hosts
);
