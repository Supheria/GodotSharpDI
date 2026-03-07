using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 为 [Provide] 标记的成员生成服务提供代码
/// </summary>
internal static class ServiceProvisionPhase
{
    /// <summary>
    /// 为单个成员生成服务提供调用语句。
    /// </summary>
    public static void GenerateMemberProvide(
        CodeFormatter f,
        MemberInfo member,
        string scopeField,
        string providerTypeName,
        string instancePrefix = "",
        bool inAsyncContext = false
    )
    {
        var memberAccess = GetMemberAccess(member, instancePrefix);
        var implType = member.MemberType.ToFullyQualifiedName();

        f.AppendLine($"// 提供服务: {implType}");

        if (member.IsAsync)
        {
            if (inAsyncContext)
                f.AppendLine(
                    $"await ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField}, __lifetime_cancellation_tokens.Token);"
                );
            else
                f.AppendLine(
                    $"_ = ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField}, __lifetime_cancellation_tokens.Token);"
                );
        }
        else
        {
            GenerateSyncProvide(f, memberAccess, implType, scopeField, providerTypeName);
        }

        f.AppendLine();
    }

    /// <summary>
    /// 生成所有异步提供辅助方法。
    /// </summary>
    public static void GenerateAsyncProviderMethods(
        CodeFormatter f,
        ImmutableArray<MemberInfo> asyncMembers,
        string providerTypeName
    )
    {
        foreach (var member in asyncMembers.Where(m => m.IsAsync))
            GenerateAsyncProviderMethod(f, member, providerTypeName);
    }

    /// <summary>
    /// 生成单个异步提供方法（实例方法）。
    /// </summary>
    private static void GenerateAsyncProviderMethod(
        CodeFormatter f,
        MemberInfo member,
        string providerTypeName
    )
    {
        var implType = member.MemberType.ToFullyQualifiedName();
        var taskTypeName = $"{GlobalNames.Task}<{implType}>";
        var methodName = $"ProvideAsync_{member.Symbol.Name}";

        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"private async {GlobalNames.Task} {methodName}("
                + $"{taskTypeName} task, {GlobalNames.IScope} scope, global::System.Threading.CancellationToken ct)"
        );
        f.BeginBlock();
        {
            // OperationCanceledException 先于 Exception 捕获，确保取消静默退出
            f.AppendLine("try");
            f.BeginBlock();
            {
                f.AppendLine("var result = await task;");
                f.AppendLine();
                // await 返回后检查 token（ExitTree 已取消）
                f.AppendLine("ct.ThrowIfCancellationRequested();");
                f.AppendLine();
                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    // CallDeferred 排队期间 token 可能再次被取消，进入后检查一次
                    f.AppendLine("if (ct.IsCancellationRequested) return;");
                    f.AppendLine(
                        $"scope.ProvideService<{implType}>(result, \"{providerTypeName}\");"
                    );
                }
                f.EndBlock(").CallDeferred();");
            }
            f.EndBlock();
            f.AppendLine("catch (global::System.OperationCanceledException)");
            f.BeginBlock();
            {
                // 节点已退出场景树，静默退出，不调用 ProvideService
                f.AppendLine(
                    "// Node exited scene tree – silent cancellation, do not call ProvideService"
                );
            }
            f.EndBlock();
            f.AppendLine("catch (global::System.Exception ex)");
            f.BeginBlock();
            {
                f.AppendLine("if (ct.IsCancellationRequested) return;");
                f.AppendLine();
                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PrintErr("
                        + $"$\"[GodotSharpDI] Async provider for {implType} threw: {{ex.Message}}\");"
                );

                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    f.AppendLine("if (ct.IsCancellationRequested) return;");
                    f.AppendLine(
                        $"scope.ProvideService<{implType}>(null, \"{providerTypeName}\");"
                    );
                }
                f.EndBlock(").CallDeferred();");
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// 生成同步服务提供代码。
    ///   成功 → scope.ProvideService&lt;T&gt;(instance, providerType)
    ///   异常 → scope.ProvideService&lt;T&gt;(null, providerType)
    /// </summary>
    private static void GenerateSyncProvide(
        CodeFormatter f,
        string memberAccess,
        string implType,
        string scopeField,
        string providerTypeName
    )
    {
        f.BeginTryCatch();
        {
            f.AppendLine($"var instance = {memberAccess};");
            f.AppendLine(
                $"{scopeField}.ProvideService<{implType}>(instance, \"{providerTypeName}\");"
            );
        }
        f.CatchBlock("ex");
        {
            f.AppendLine(
                $"{GlobalNames.GodotGD}.PrintErr("
                    + $"$\"[GodotSharpDI] Provider for {implType} threw: {{ex.Message}}\");"
            );
            f.AppendLine($"{scopeField}.ProvideService<{implType}>(null, \"{providerTypeName}\");");
        }
        f.EndTryCatch();
    }

    /// <summary>
    /// 获取成员访问表达式。
    /// </summary>
    private static string GetMemberAccess(MemberInfo member, string instancePrefix)
    {
        var prefix = string.IsNullOrEmpty(instancePrefix) ? "" : $"{instancePrefix}.";
        return member.Kind switch
        {
            MemberKind.ProvideField => $"{prefix}{member.Symbol.Name}",
            MemberKind.ProvideProperty => $"{prefix}{member.Symbol.Name}",
            MemberKind.ProvideMethod => $"{prefix}{member.Symbol.Name}()",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
