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
/// v1.3.0 重构：
///   旧设计：TCS → ContinueWith（线程池）→ CallDeferred → 主线程回调，需要 _diGeneration 双重 gate
///   新设计：直接向 [Inject] 成员对应的 List&lt;Action&lt;bool&gt;&gt; 注册回调，
///           ResolveDependencies() 在主线程触发回调时直接调用，零跨线程跳转。
///
/// 消除的复杂性：
///   - 不再需要 volatile _diGeneration 计数器
///   - 不再需要 Interlocked.Decrement 原子操作
///   - 不再需要 ContinueWith + TaskScheduler.Default
///   - 不再需要 CallDeferred 回到主线程（本就在主线程）
///   - 不再需要双重 Generation 检查
///   ExitTree 时只需 callbacks.Clear()，所有未触发的回调自动失效。
/// </summary>
internal static class WaitForPhase
{
    /// <summary>
    /// 为单个 Provide 成员生成 WaitFor 等待代码。
    /// 在 ProvideServices() 方法体中调用，生成向各依赖回调列表注册 lambda 的代码。
    /// 当所有依赖都就绪（或失败）时，在主线程直接调用 OnXxxWaitForResolved()。
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
        var remainingVarName = $"_{memberName}_remaining";
        var resolvedCallbackName = $"On{memberName}WaitForResolved";

        f.AppendLine($"// WaitFor deps for {memberName}: {string.Join(", ", waitForDeps)}");

        // 本地计数器：全部在主线程上递减，无需 Interlocked
        f.AppendLine($"var {remainingVarName} = {waitForDeps.Length};");
        f.AppendLine();

        foreach (var depName in waitForDeps)
        {
            var depMember = allMembers.FirstOrDefault(m => m.Symbol.Name == depName);
            if (depMember == null)
            {
                f.AppendLine($"// Error: WaitFor field '{depName}' not found in members");
                continue;
            }

            var listName = NamingHelper.GetInjectionCallbackListName(depName);

            f.AppendLine($"// WaitFor: register main-thread callback for '{depName}'");

            // 向回调列表注册 lambda；ResolveDependencies() 在主线程触发时直接调用
            f.AppendLine($"{listName}.Add(__ok =>");
            f.BeginBlock();
            {
                f.AppendLine("if (!__ok)");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"{GlobalNames.GodotGD}.PrintErr("
                            + $"$\"[GodotSharpDI] WaitFor: dependency '{depName}' for '{memberName}' failed\");"
                    );
                }
                f.EndBlock();
                // 无论成功或失败都递减；归零时触发回调（与旧设计行为一致）
                f.AppendLine($"if (--{remainingVarName} == 0)");
                f.BeginBlock();
                {
                    // 已在主线程上，直接调用 – 无需 CallDeferred
                    f.AppendLine($"_ = {resolvedCallbackName}().ContinueWith(t =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (t.IsFaulted)");
                        f.BeginBlock();
                        {
                            f.AppendLine(
                                $"{GlobalNames.GodotGD}.PrintErr("
                                    + $"$\"[GodotSharpDI] WaitFor callback '{resolvedCallbackName}' threw: {{t.Exception?.GetBaseException().Message}}\");"
                            );
                        }
                        f.EndBlock();
                    }
                    f.EndBlock(", global::System.Threading.Tasks.TaskScheduler.Default);");
                }
                f.EndBlock();
            }
            f.EndBlock(");");
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
