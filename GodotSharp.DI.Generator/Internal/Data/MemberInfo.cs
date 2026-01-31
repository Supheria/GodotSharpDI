using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 成员信息
/// </summary>
internal sealed record MemberInfo(
    ISymbol Symbol,
    Location Location,
    MemberKind Kind,
    INamedTypeSymbol MemberType,
    ImmutableArray<INamedTypeSymbol> ExposedTypes
)
{
    public bool IsInjectMember { get; } =
        Kind == MemberKind.InjectField || Kind == MemberKind.InjectProperty;
    public bool IsSingletonMember { get; } =
        Kind == MemberKind.SingletonField || Kind == MemberKind.SingletonProperty;
}
