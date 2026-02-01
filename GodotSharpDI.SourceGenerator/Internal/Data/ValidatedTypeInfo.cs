using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 类型信息（验证后）
/// </summary>
internal sealed record ValidatedTypeInfo(
    INamedTypeSymbol Symbol,
    Location Location,
    TypeRole Role,
    bool ImplementsIServicesReady,
    bool IsNode,
    ImmutableArray<MemberInfo> Members,
    ConstructorInfo? Constructor,
    ModulesInfo? ModulesInfo
);
