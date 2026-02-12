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
    /// <param name="f">代码格式化器</param>
    /// <param name="provideMember">提供服务的成员信息</param>
    /// <param name="allMembers">所有成员信息（用于查找 WaitFor 依赖的类型）</param>
    /// <param name="scopeField">Scope 变量名称</param>
    /// <param name="onAllResolved">该成员的所有 WaitFor 依赖就绪后的回调</param>
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

        // 为每个成员生成独立的 remaining 计数器
        var remainingVarName = $"_{memberName}_waitForRemaining";
        var resolvedCallbackName = $"On{memberName}WaitForResolved";

        f.AppendLine($"// 等待 {memberName} 的 WaitFor 依赖: {string.Join(", ", waitForDeps)}");
        f.AppendLine($"var {remainingVarName} = {waitForDeps.Length};");
        f.AppendLine();

        // 为每个依赖注册解析回调
        foreach (var depName in waitForDeps)
        {
            // 查找依赖字段的类型
            var depMember = allMembers.FirstOrDefault(m => m.Symbol.Name == depName);
            if (depMember == null)
            {
                // 这种情况应该在验证阶段被捕获，这里添加防御性代码
                f.AppendLine($"// 错误: 找不到依赖字段 {depName}");
                continue;
            }

            var depType = depMember.MemberType.ToFullyQualifiedName();

            f.AppendLine($"// 监听依赖: {depName} ({depType})");
            f.AppendLine($"{scopeField}.ResolveDependency<{depType}>(");
            f.BeginLevel();
            {
                // onResolved 回调
                f.AppendLine("(dependency) =>");
                f.BeginBlock();
                {
                    f.AppendLine($"if (--{remainingVarName} == 0)");
                    f.BeginBlock();
                    {
                        // 使用 _ = 启动异步流程但不等待
                        f.AppendLine($"_ = {resolvedCallbackName}();");
                    }
                    f.EndBlock();
                }
                f.EndBlock(",");

                // onFailed 回调
                f.AppendLine("(error) =>");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"{GlobalNames.GodotGD}.PrintErr($\"[{memberName}] WaitFor 依赖 '{depName}' 解析失败: {{error}}\");"
                    );
                    // 依赖失败时也减少计数，避免死锁
                    f.AppendLine($"if (--{remainingVarName} == 0)");
                    f.BeginBlock();
                    {
                        // 即使失败也启动异步流程
                        f.AppendLine($"_ = {resolvedCallbackName}();");
                    }
                    f.EndBlock();
                }
                f.EndBlock(",");

                // requestorType
                f.AppendLine($"requestorType: \"{memberName} (WaitFor)\"");
            }
            f.EndLevel();
            f.AppendLine(");");
            f.AppendLine();
        }

        f.AppendLine("return;");
        f.AppendLine();

        // 生成异步回调方法
        f.AppendLine($"async {GlobalNames.Task} {resolvedCallbackName}()");
        f.BeginBlock();
        {
            f.AppendLine($"// {memberName} 的所有 WaitFor 依赖已就绪，开始提供服务");

            // 在异步上下文中调用服务提供代码
            onAllResolved();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// 如果 Host 和 Provider 也要实现 IDependenciesResolved 则使用这个方法
    /// </summary>
    [Obsolete("使用 GenerateForMember 替代，以支持每个成员独立的 remaining 计数")]
    public static void Generate(
        CodeFormatter f,
        ImmutableArray<string> waitForDeps,
        Action onAllResolved
    )
    {
        if (waitForDeps.IsEmpty)
        {
            onAllResolved();
            return;
        }

        f.AppendLine($"// 等待 WaitFor 依赖: {string.Join(", ", waitForDeps)}");
        f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
        f.BeginBlock();
        {
            // 检查所有 WaitFor 字段不为 null
            foreach (var dep in waitForDeps)
            {
                f.AppendLine($"if ({dep} == null)");
                f.BeginBlock();
                {
                    f.AppendLine($"{GlobalNames.GodotGD}.PrintErr(\"WaitFor 依赖未就绪: {dep}\");");
                    f.AppendLine("return;");
                }
                f.EndBlock();
            }

            f.AppendLine("// 所有 WaitFor 依赖已就绪");
            onAllResolved();
        }
        f.EndBlock(").CallDeferred();");
    }
}
