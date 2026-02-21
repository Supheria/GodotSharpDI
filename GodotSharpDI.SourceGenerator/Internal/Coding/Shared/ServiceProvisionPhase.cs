using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 阶段 3：为 [Provide] 标记的成员生成服务提供代码
///
/// v1.3.0：移除 ResolutionResult。
///   成功 → scope.ProvideService&lt;T&gt;(instance)   （传递实例本身）
///   失败 → scope.ProvideService&lt;T&gt;(null)        （null 表示创建失败）
///
/// FIX3：异步提供方法改为实例方法，以访问 _diGeneration 字段实现回调取消。
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
                    $"await ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField});"
                );
            else
                f.AppendLine(
                    $"_ = ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField});"
                );
        }
        else
        {
            GenerateSyncProvide(f, memberAccess, implType, scopeField);
        }

        f.AppendLine();
    }

    /// <summary>
    /// 生成所有异步提供辅助方法。
    /// </summary>
    public static void GenerateAsyncProviderMethods(
        CodeFormatter f,
        ImmutableArray<MemberInfo> asyncMembers
    )
    {
        foreach (var member in asyncMembers.Where(m => m.IsAsync))
            GenerateAsyncProviderMethod(f, member);
    }

    /// <summary>
    /// 生成单个异步提供方法（实例方法）。
    ///
    /// FIX3：改为实例方法以访问 _diGeneration 字段。
    ///   成功 → scope.ProvideService&lt;T&gt;(result)  （await 返回的实例）
    ///   异常 → scope.ProvideService&lt;T&gt;(null)    （null 表示创建失败）
    ///   两种情况均通过 CallDeferred 回到 Godot 主线程后执行。
    /// </summary>
    private static void GenerateAsyncProviderMethod(CodeFormatter f, MemberInfo member)
    {
        var implType = member.MemberType.ToFullyQualifiedName();
        var taskTypeName = $"{GlobalNames.Task}<{implType}>";
        var methodName = $"ProvideAsync_{member.Symbol.Name}";

        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"private async {GlobalNames.Task} {methodName}("
                + $"{taskTypeName} task, {GlobalNames.IScope} scope)"
        );
        f.BeginBlock();
        {
            // 捕获当前 Generation，用于判断回调是否已失效
            f.AppendLine("var capturedGen = _diGeneration;");
            f.AppendLine();

            f.BeginTryCatch();
            {
                f.AppendLine("var result = await task;");
                f.AppendLine();
                // await 返回后（可能在线程池线程），先检查 Generation
                f.AppendLine("if (_diGeneration != capturedGen) return;");
                f.AppendLine();
                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    // 进入 Deferred 回调前再次校验（排队到执行期间可能再次重入）
                    f.AppendLine("if (_diGeneration != capturedGen) return;");
                    f.AppendLine($"scope.ProvideService<{implType}>(result);");
                }
                f.EndBlock(").CallDeferred();");
            }
            f.CatchBlock("ex");
            {
                f.AppendLine("if (_diGeneration != capturedGen) return;");
                f.AppendLine();
                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PrintErr("
                        + $"$\"[GodotSharpDI] Async provider for {implType} threw: {{ex.Message}}\");"
                );

                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    f.AppendLine("if (_diGeneration != capturedGen) return;");
                    f.AppendLine($"scope.ProvideService<{implType}>(null);");
                }
                f.EndBlock(").CallDeferred();");
            }
            f.EndTryCatch();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// 生成同步服务提供代码。
    ///   成功 → scope.ProvideService&lt;T&gt;(instance)
    ///   异常 → scope.ProvideService&lt;T&gt;(null)
    /// </summary>
    private static void GenerateSyncProvide(
        CodeFormatter f,
        string memberAccess,
        string implType,
        string scopeField
    )
    {
        f.BeginTryCatch();
        {
            f.AppendLine($"var instance = {memberAccess};");
            f.AppendLine($"{scopeField}.ProvideService<{implType}>(instance);");
        }
        f.CatchBlock("ex");
        {
            f.AppendLine(
                $"{GlobalNames.GodotGD}.PrintErr("
                    + $"$\"[GodotSharpDI] Provider for {implType} threw: {{ex.Message}}\");"
            );
            f.AppendLine($"{scopeField}.ProvideService<{implType}>(null);");
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
            MemberKind.ProvideProperty => $"{prefix}{member.Symbol.Name}",
            MemberKind.ProvideMethod => $"{prefix}{member.Symbol.Name}()",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
