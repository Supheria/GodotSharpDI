using System.Collections.Immutable;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// Host 和 User 解析依赖时都会用到的内容
/// </summary>
internal static class DependencyResolveGenerator
{
    /// <summary>
    /// 生成调用 OnDependencyResolved 的代码片段
    /// 用于嵌入到依赖注入回调中
    /// </summary>
    public static void GenerateResolvedCallback(CodeFormatter f, string memberTypeName)
    {
        f.AppendLine($"OnDependencyResolved<{memberTypeName}>();");
    }

    /// <summary>
    /// 生成设置注入准备标识的代码片段
    /// 用于嵌入到依赖注入成功回调中
    /// </summary>
    public static void GenerateSetInjectionReady(
        CodeFormatter f,
        string memberName,
        string memberType
    )
    {
        var fieldName = NamingHelper.GetInjectionReadyFieldName(memberName);
        f.AppendLine($"{memberName} ??= ({memberType})result.Instance!;");
        f.AppendLine($"{fieldName} = true;");
    }
}
