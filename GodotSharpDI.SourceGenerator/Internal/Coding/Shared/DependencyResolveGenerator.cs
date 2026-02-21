using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// Host 和 User 解析依赖时共用的代码片段生成器
/// </summary>
internal static class DependencyResolveGenerator
{
    /// <summary>
    /// 生成调用 OnDependencyResolved 的代码片段（IDependenciesResolved 接口用）
    /// </summary>
    public static void GenerateResolvedCallback(CodeFormatter f, string memberTypeName)
    {
        f.AppendLine($"OnDependencyResolved<{memberTypeName}>();");
    }

    /// <summary>
    /// 生成注入成功时设置成员值和 ready 标识的代码片段。
    /// 调用上下文中局部变量名必须为 "instance"（即 ResolveDependency 回调的参数名）。
    /// </summary>
    public static void GenerateSetInjectionReady(
        CodeFormatter f,
        string memberName,
        string memberType
    )
    {
        var fieldName = NamingHelper.GetInjectionReadyFieldName(memberName);
        // instance 是 TExposed? 类型，已确认非 null 才进入此分支，无需强制转换
        f.AppendLine($"{memberName} ??= instance;");
        f.AppendLine($"{fieldName} = true;");
    }
}
