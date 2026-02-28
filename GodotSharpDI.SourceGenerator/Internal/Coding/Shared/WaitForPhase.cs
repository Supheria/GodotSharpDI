using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成 WaitFor 依赖等待代码
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
                    // OnXxxWaitForResolved() 是 async 本地函数，其内部若包含 await，
                    // 续体可能在线程池线程上完成。ContinueWith 使用 TaskScheduler.Default，
                    // 因此 body 同样在线程池线程执行。
                    // GD.PrintErr 本身是线程安全的，但为了与项目其余部分保持一致
                    // （所有 Godot API 调用均在主线程），通过 Callable.From().CallDeferred()
                    // 将错误日志派发回 Godot 主线程，避免未来扩展时引入潜在的线程安全问题。
                    f.AppendLine($"_ = {resolvedCallbackName}().ContinueWith(t =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (t.IsFaulted)");
                        f.BeginBlock();
                        {
                            // 捕获错误信息到局部变量（ContinueWith body 在线程池，
                            // 不能直接访问 t 以外的 Godot 对象）
                            f.AppendLine("var __errMsg = t.Exception?.GetBaseException().Message;");
                            f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                            f.BeginBlock();
                            {
                                f.AppendLine(
                                    $"{GlobalNames.GodotGD}.PrintErr("
                                        + $"$\"[GodotSharpDI] WaitFor callback '{resolvedCallbackName}' threw: {{__errMsg}}\");"
                                );
                            }
                            f.EndBlock(").CallDeferred();");
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
