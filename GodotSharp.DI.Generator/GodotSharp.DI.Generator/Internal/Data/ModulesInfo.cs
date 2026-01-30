using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 模块信息
/// </summary>
internal sealed record ModulesInfo(
    ImmutableArray<ITypeSymbol> Services,
    ImmutableArray<ITypeSymbol> Hosts
);
