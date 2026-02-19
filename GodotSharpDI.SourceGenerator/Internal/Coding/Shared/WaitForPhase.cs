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
/// P3: 复用 InjectionGenerator 生成的 TCS，避免双重 ResolveDependency 注册
/// P1: requestorType 采用 "GDI_WF:{providerService}:{member}" 协议，供运行时死锁检测
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
        string scopeField = GlobalNames.LocalScope,
        Action? onAllResolved = null
    )
    {
        var waitForDeps = provideMember.WaitFor;

        if (waitForDeps.IsEmpty)
        {
            // 没有 WaitFor，直接调用回调
            onAllResolved?.Invoke();
            return;
        }

        var memberName = provideMember.Symbol.Name;
        var remainingVarName = $"_{memberName}_waitForRemaining";
        var resolvedCallbackName = $"On{memberName}WaitForResolved";

        // P1-runtime: 取第一个暴露类型名作为 provider 标识
        var providerSvcName = provideMember.ExposedTypes.IsEmpty
            ? memberName
            : provideMember.ExposedTypes[0].Name;

        f.AppendLine($"// WaitFor deps for {memberName}: {string.Join(", ", waitForDeps)}");
        f.AppendLine($"var {remainingVarName} = {waitForDeps.Length};");
        f.AppendLine();

        // P3: 为每个 WaitFor 依赖复用 InjectionGenerator 生成的 TCS
        foreach (var depName in waitForDeps)
        {
            var depMember = allMembers.FirstOrDefault(m => m.Symbol.Name == depName);
            if (depMember == null)
            {
                f.AppendLine($"// Error: WaitFor field '{depName}' not found");
                continue;
            }

            var tcsName = NamingHelper.GetInjectionTcsName(depName);

            f.AppendLine($"// WaitFor: reuse injection TCS for '{depName}' (no duplicate ResolveDependency)");
            f.AppendLine($"_ = {tcsName}.Task.ContinueWith(completedTask =>");
            f.BeginBlock();
            {
                f.AppendLine("var result = completedTask.Result;");
                f.AppendLine("if (result.IsSuccess)");
                f.BeginBlock();
                {
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
                        $"{GlobalNames.GodotGD}.PrintErr($\"[{memberName}] WaitFor dependency " +
                        $"'{depName}' failed: {{result.ErrorMessage}}\");"
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
            f.AppendLine("    TaskScheduler.FromCurrentSynchronizationContext());");
            f.AppendLine();
        }
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
            f.AppendLine($"// All WaitFor deps for {memberName} are ready");
            f.AppendLine();
            onAllResolved();
        }
        f.EndBlock();
        f.AppendLine();
    }
}

