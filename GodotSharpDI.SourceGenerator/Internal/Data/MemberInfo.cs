using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 成员信息
/// </summary>
internal sealed record MemberInfo(
    ISymbol Symbol,
    Location Location,
    MemberKind Kind,
    INamedTypeSymbol MemberType,
    ImmutableArray<INamedTypeSymbol> ExposedTypes,
    bool HasFailureCallback,
    bool HasReadyCallback,
    ImmutableArray<string> WaitFor, // 新增：等待的依赖字段名称数组
    bool IsAsync = false, // 新增：是否是异步成员
    bool UsesProvide = false // 新增：是否使用 Provide 特性（而非 Singleton）
)
{
    public bool IsInjectMember { get; } =
        Kind == MemberKind.InjectField || Kind == MemberKind.InjectProperty;
    public bool IsProvideMember { get; } =
        Kind == MemberKind.ProvideProperty || Kind == MemberKind.ProvideMethod;

    /// <summary>
    /// 获取此成员名称
    /// </summary>
    public string Name => Symbol.Name;

    /// <summary>
    /// 是否有 WaitFor 依赖
    /// </summary>
    public bool HasWaitFor => !WaitFor.IsEmpty;
}
