using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成 WaitFor 依赖等待代码（重构版本）
/// 每个 Provide 成员拥有独立的 _remaining 计数器
/// </summary>
internal static class WaitForPhase
{
    /// <summary>
    /// 为单个 Provide 成员生成独立的 WaitFor 等待代码
    /// </summary>
    public static void GenerateForMember(
        CodeFormatter f,
        MemberInfo provideMember,
        ImmutableArray<MemberInfo> allMembers,
        string scopeField,
        Action onAllResolved
    )
    {
        var waitForDeps = provideMember.WaitFor;

        if (waitForDeps.IsEmpty)
        {
            // 没有 WaitFor，直接调用回调
            onAllResolved();
            return;
        }

        var memberName = provideMember.Symbol.Name;
        var remainingVarName = $"_{memberName}_waitForRemaining";
        var resolvedCallbackName = $"On{memberName}WaitForResolved";

        f.AppendLine($"// 等待 {memberName} 的 WaitFor 依赖: {string.Join(", ", waitForDeps)}");
        f.AppendLine($"var {remainingVarName} = {waitForDeps.Length};");
        f.AppendLine();

        // 为每个依赖注册解析回调
        foreach (var depName in waitForDeps)
        {
            var depMember = allMembers.FirstOrDefault(m => m.Symbol.Name == depName);
            if (depMember == null)
            {
                f.AppendLine($"// 错误: 找不到依赖字段 {depName}");
                continue;
            }

            var depType = depMember.MemberType.ToFullyQualifiedName();

            f.AppendLine($"// 监听依赖: {depName} ({depType})");
            f.AppendLine($"{scopeField}.ResolveDependency<{depType}>(");
            f.BeginLevel();
            {
                f.AppendLine("(result) =>");
                f.BeginBlock();
                {
                    f.AppendLine("if (result.IsSuccess)");
                    f.BeginBlock();
                    {
                        IDependenciesResolvedGenerator.GenerateSetInjectionReady(
                            f,
                            depName,
                            depType
                        );
                        f.AppendLine($"if (--{remainingVarName} == 0)");
                        f.BeginBlock();
                        {
                            f.AppendLine($"_ = {resolvedCallbackName}();");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock();
                    f.AppendLine("else");
                    f.BeginBlock();
                    {
                        f.AppendLine(
                            $"{GlobalNames.GodotGD}.PrintErr($\"[{memberName}] WaitFor 依赖 '{depName}' 解析失败: {{result.ErrorMessage}}\");"
                        );
                        f.AppendLine($"if (--{remainingVarName} == 0)");
                        f.BeginBlock();
                        {
                            f.AppendLine($"_ = {resolvedCallbackName}();");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock();
                }
                f.EndBlock(",");

                f.AppendLine($"requestorType: \"{memberName} (WaitFor)\"");
            }
            f.EndLevel();
            f.AppendLine(");");
            f.AppendLine();
        }

        // 不再在这里添加 return 和 local function 定义
        // 而是返回一个委托，让调用者统一生成
    }

    /// <summary>
    /// 生成 WaitFor 回调的 local function 定义
    /// </summary>
    public static void GenerateLocalFunction(
        CodeFormatter f,
        MemberInfo provideMember,
        Action onAllResolved
    )
    {
        var memberName = provideMember.Symbol.Name;
        var resolvedCallbackName = $"On{memberName}WaitForResolved";

        f.AppendLine($"async {GlobalNames.Task} {resolvedCallbackName}()");
        f.BeginBlock();
        {
            f.AppendLine($"// {memberName} 的所有 WaitFor 依赖已就绪，开始提供服务");
            f.AppendLine();
            onAllResolved();
        }
        f.EndBlock();
        f.AppendLine();
    }
}
