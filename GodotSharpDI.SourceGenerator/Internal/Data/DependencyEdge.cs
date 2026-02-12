using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 依赖边 - 包含源成员信息以支持更精确的循环依赖检测
/// </summary>
internal sealed record DependencyEdge(
    ITypeSymbol TargetType,
    Location Location,
    DependencySource Source,
    // 新增：源成员名称（用于WaitFor依赖的精确追踪）
    string? SourceMemberName = null,
    // 新增：源成员提供的服务类型（用于WaitFor场景）
    ITypeSymbol? SourceProvidedType = null
);
