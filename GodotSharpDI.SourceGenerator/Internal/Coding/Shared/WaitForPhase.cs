using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成 WaitFor 依赖等待代码
///
/// FIX1 — _remaining 改用 Interlocked.Decrement 原子递减，修复并发竞态
/// FIX2 — 废弃 FromCurrentSynchronizationContext，改用 TaskScheduler.Default + CallDeferred
/// FIX3 — Generation ID 检查，使节点退出/重入时的旧回调自动失效
/// v1.3.0 — TCS 类型改为 TaskCompletionSource&lt;bool&gt;（已移除 ResolutionResult）
///           ContinueWith 结果为 bool：true = 成功，false = 失败
/// </summary>
internal static class WaitForPhase
{
    /// <summary>
    /// 为单个 Provide 成员生成 WaitFor 等待代码。
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
            onAllResolved?.Invoke();
            return;
        }

        var memberName = provideMember.Symbol.Name;
        var remainingVarName = $"_{memberName}_waitForRemaining";
        var capturedGenVarName = $"_{memberName}_capturedGen";
        var resolvedCallbackName = $"On{memberName}WaitForResolved";

        f.AppendLine($"// WaitFor deps for {memberName}: {string.Join(", ", waitForDeps)}");

        // FIX1：声明为 int，通过 Interlocked.Decrement 操作
        f.AppendLine($"var {remainingVarName} = {waitForDeps.Length};");

        // FIX3：捕获当前 Generation 快照
        f.AppendLine($"var {capturedGenVarName} = _diGeneration;");
        f.AppendLine();

        foreach (var depName in waitForDeps)
        {
            var depMember = allMembers.FirstOrDefault(m => m.Symbol.Name == depName);
            if (depMember == null)
            {
                f.AppendLine($"// Error: WaitFor field '{depName}' not found in members");
                continue;
            }

            var tcsName = NamingHelper.GetInjectionTcsName(depName);

            f.AppendLine($"// WaitFor: await TCS for '{depName}' (bool: true=success)");

            // FIX2：使用 TaskScheduler.Default（Godot 不保证存在标准 SynchronizationContext）
            f.AppendLine($"_ = {tcsName}.Task.ContinueWith(completedTask =>");
            f.BeginBlock();
            {
                // FIX3：收到旧 Generation 的回调直接丢弃
                f.AppendLine($"if (_diGeneration != {capturedGenVarName}) return;");
                f.AppendLine();

                // v1.3.0：Result 为 bool（true = 注入成功）
                f.AppendLine("var succeeded = completedTask.Result;");
                f.AppendLine("if (succeeded)");
                f.BeginBlock();
                {
                    // FIX1：原子递减，归零时触发回调
                    f.AppendLine(
                        $"if (global::System.Threading.Interlocked.Decrement(ref {remainingVarName}) == 0)"
                    );
                    f.BeginBlock();
                    {
                        // FIX2：通过 CallDeferred 派发回 Godot 主线程
                        f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                        f.BeginBlock();
                        {
                            // FIX3：进入 Deferred 前再次校验
                            f.AppendLine($"if (_diGeneration != {capturedGenVarName}) return;");
                            f.AppendLine($"_ = {resolvedCallbackName}().ContinueWith(t =>");
                            f.BeginBlock();
                            {
                                f.AppendLine("if (t.IsFaulted)");
                                f.BeginBlock();
                                {
                                    f.AppendLine(
                                        $"{GlobalNames.GodotGD}.PrintErr("
                                            + $"$\"[GodotSharpDI] WaitFor callback '{resolvedCallbackName}' threw: {{t.Exception?.GetBaseException().Message}}\");");
                                }
                                f.EndBlock();
                            }
                            f.EndBlock($", {GlobalNames.Task}Scheduler.Default);");
                        }
                        f.EndBlock(").CallDeferred();");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
                f.AppendLine("else");
                f.BeginBlock();
                {
                    // 依赖失败时同样递减，确保计数归零后仍能触发回调（以便上层决策）
                    f.AppendLine(
                        $"{GlobalNames.GodotGD}.PrintErr("
                            + $"$\"[GodotSharpDI] WaitFor: dependency '{depName}' for '{memberName}' failed\");"
                    );
                    f.AppendLine(
                        $"if (global::System.Threading.Interlocked.Decrement(ref {remainingVarName}) == 0)"
                    );
                    f.BeginBlock();
                    {
                        f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                        f.BeginBlock();
                        {
                            f.AppendLine($"if (_diGeneration != {capturedGenVarName}) return;");
                            f.AppendLine($"_ = {resolvedCallbackName}().ContinueWith(t =>");
                            f.BeginBlock();
                            {
                                f.AppendLine("if (t.IsFaulted)");
                                f.BeginBlock();
                                {
                                    f.AppendLine(
                                        $"{GlobalNames.GodotGD}.PrintErr("
                                            + $"$\"[GodotSharpDI] WaitFor callback '{resolvedCallbackName}' threw: {{t.Exception?.GetBaseException().Message}}\");");
                                }
                                f.EndBlock();
                            }
                            f.EndBlock($", {GlobalNames.Task}Scheduler.Default);");
                        }
                        f.EndBlock(").CallDeferred();");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock(",");
            f.AppendLine("    global::System.Threading.Tasks.TaskScheduler.Default);");
            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成 WaitFor 回调的本地函数定义。
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
            f.AppendLine($"// All WaitFor deps for '{memberName}' have settled");
            f.AppendLine();
            onAllResolved();
        }
        f.EndBlock();
        f.AppendLine();
    }
}
