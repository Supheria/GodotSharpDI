using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

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
