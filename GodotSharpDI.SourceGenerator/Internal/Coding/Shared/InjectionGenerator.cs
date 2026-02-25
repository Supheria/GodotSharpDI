using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

internal static class InjectionGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        var injectMembers = node
            .ValidatedTypeInfo.Members.Where(m => m.IsInjectMember)
            .ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
        {
            // _diGeneration 字段无论是否有 Inject 成员都需要生成
            // WaitForPhase 和异步 Provider 均依赖此字段
            GenerateDiGenerationField(f);
            f.AppendLine();

            if (!injectMembers.IsEmpty)
            {
                var membersWithFailureCallback = injectMembers
                    .Where(m => m.HasFailureCallback)
                    .ToArray();
                if (membersWithFailureCallback.Length > 0)
                {
                    GenerateFailureCallbackDeclarations(f, membersWithFailureCallback);
                    f.AppendLine();
                }

                var membersWithReadyCallback = injectMembers
                    .Where(m => m.HasReadyCallback)
                    .ToArray();
                if (membersWithReadyCallback.Length > 0)
                {
                    GenerateReadyCallbackDeclarations(f, membersWithReadyCallback);
                    f.AppendLine();
                }

                GenerateInjectionReadyProperties(f, injectMembers);
                GenerateIsAllDependenciesReadyProperty(f, injectMembers);

                // TCS 字段同时被 WaitForPhase 和 ResolveDependencies 引用
                GenerateInjectionTcsFields(f, injectMembers);

                if (node.ValidatedTypeInfo.ImplementsIDependenciesResolved)
                    GenerateIDependenciesResolvedSpecific(f, injectMembers);
            }

            GenerateResetInjectionState(f, injectMembers);
            GenerateResolveDependencies(f, node.ValidatedTypeInfo, injectMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Inject.g.cs", f.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // 公开方法（供其他 Generator 调用）
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 生成 _diGeneration 字段。
    /// 声明为 volatile，确保线程池中的 ContinueWith 也能读到最新值。
    /// </summary>
    public static void GenerateDiGenerationField(CodeFormatter f)
    {
        f.AppendHiddenMemberCommentAndAttribute(
            "Generation counter – incremented on ExitTree/EnterTree to invalidate in-flight callbacks"
        );
        f.AppendLine("private volatile int _diGeneration = 0;");
    }

    /// <summary>
    /// 生成 TCS 实例字段。
    /// 类型为 TaskCompletionSource&lt;bool&gt;：true = 注入成功，false = 注入失败。
    /// </summary>
    public static void GenerateInjectionTcsFields(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var tcsName = NamingHelper.GetInjectionTcsName(member.Symbol.Name);
            f.AppendHiddenMemberCommentAndAttribute(
                $"WaitFor synchronization TCS for {member.Symbol.Name} (true=success, false=failure)"
            );
            f.AppendLine(
                $"private global::System.Threading.Tasks.TaskCompletionSource<{GlobalNames.Bool}>"
                    + $" {tcsName} = new();"
            );
            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成 ResetInjectionState() 方法。
    /// 在 EnterTree / ExitTree 时调用：
    ///   1. 递增 _diGeneration（使已有的异步回调失效）
    ///   2. 对旧 TCS 调用 TrySetResult(false)，确保所有 ContinueWith 回调不会永远挂起
    ///      （回调执行时会检测 _diGeneration 不匹配并静默退出）
    ///   3. 创建新 TCS 和重置 ready 标识
    /// </summary>
    public static void GenerateResetInjectionState(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "Reset injection state on EnterTree/ExitTree to cancel in-flight async operations"
        );
        f.AppendLine("private void ResetInjectionState()");
        f.BeginBlock();
        {
            f.AppendLine("global::System.Threading.Interlocked.Increment(ref _diGeneration);");

            if (!injectMembers.IsEmpty)
            {
                f.AppendLine();
                f.AppendLine("// Settle old TCS instances so any awaiting ContinueWith callbacks");
                f.AppendLine(
                    "// can complete and exit (they will check _diGeneration and discard)."
                );
                f.AppendLine("// This prevents Task leaks from TCS objects that would otherwise");
                f.AppendLine("// never transition to a completed state.");
            }

            foreach (var member in injectMembers)
            {
                var tcsName = NamingHelper.GetInjectionTcsName(member.Symbol.Name);
                var readyField = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                // 先终结旧 TCS，再创建新的
                f.AppendLine($"{tcsName}.TrySetResult(false);");
                f.AppendLine(
                    $"{tcsName} = new global::System.Threading.Tasks"
                        + $".TaskCompletionSource<{GlobalNames.Bool}>();"
                );
                f.AppendLine($"{readyField} = false;");
            }
        }
        f.EndBlock();
        f.AppendLine();
    }

    public static void GenerateInjectionReadyProperties(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Whether {member.Symbol.Name} has been successfully injected</summary>"
            );
            f.AppendLine($"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]");
            f.AppendLine($"private {GlobalNames.Bool} {fieldName} {{ get; set; }} = false;");
            f.AppendLine();
        }
    }

    public static void GenerateIsAllDependenciesReadyProperty(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        if (injectMembers.IsEmpty)
        {
            f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady => true;");
            return;
        }

        var fAttr = f.CreateFromCurrentLevel();
        var fVal = f.CreateFromCurrentLevel();
        fVal.BeginLevel();
        {
            for (int i = 0; i < injectMembers.Length; i++)
            {
                var member = injectMembers[i];
                fAttr.AppendLine(
                    $"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]"
                );
                var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                if (i > 0)
                {
                    fVal.AppendLine();
                    fVal.AppendRaw($"&& {fieldName}", true);
                }
                else
                {
                    fVal.AppendRaw($"{fieldName}", true);
                }
            }
            fVal.AppendRaw(";");
        }
        fVal.EndLevel();

        f.AppendLine(
            "/// <summary>Whether all Inject members have been successfully injected</summary>"
        );
        f.AppendRaw(fAttr.ToString());
        f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady =>");
        f.AppendRaw(fVal.ToString());
        f.AppendLine();
    }

    // ──────────────────────────────────────────────────────────────
    // 私有方法
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 生成 FailureCallback 的 partial 方法声明。
    /// v1.3.0：ErrorMessage 已废弃，因此无 string 参数。
    /// </summary>
    private static void GenerateFailureCallbackDeclarations(CodeFormatter f, MemberInfo[] members)
    {
        foreach (var member in members)
        {
            var methodName = NamingHelper.GetFailureCallbackMethodName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Called when injection of {member.Symbol.Name} fails</summary>"
            );
            f.AppendLine($"partial void {methodName}();");
            f.AppendLine();
        }
    }

    private static void GenerateReadyCallbackDeclarations(CodeFormatter f, MemberInfo[] members)
    {
        foreach (var member in members)
        {
            var methodName = NamingHelper.GetReadyCallbackMethodName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Called when injection of {member.Symbol.Name} succeeds</summary>"
            );
            f.AppendLine($"partial void {methodName}();");
            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成 ResolveDependencies() 方法。
    ///
    /// 对每个 Inject 成员调用 scope.ResolveDependency&lt;T&gt;(instance =&gt; { ... })。
    /// 回调参数 "instance" 类型为 TExposed?：
    ///   null     → 解析失败
    ///   非 null  → 解析成功，值即为实际服务实例
    /// </summary>
    private static void GenerateResolveDependencies(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembersList
    )
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ResolveDependencies()");
        f.BeginBlock();
        {
            f.AppendLine($"var {GlobalNames.LocalScope} = GetParentScope();");
            f.AppendLine($"if ({GlobalNames.LocalScope} is null)");
            f.BeginBlock();
            {
                f.PrintError(
                    $"$\"[GodotSharpDI] {validatedType.Symbol.Name}: Cannot find parent Scope in scene tree.\""
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            foreach (var member in injectMembersList)
            {
                var memberType = member.MemberType.ToFullyQualifiedName();
                var memberName = member.Symbol.Name;
                var tcsName = NamingHelper.GetInjectionTcsName(memberName);

                f.AppendLine($"{GlobalNames.LocalScope}.ResolveDependency<{memberType}>(");
                f.BeginLevel();
                {
                    // 参数名 "instance" 与 DependencyResolveGenerator.GenerateSetInjectionReady 对应
                    f.AppendLine("instance =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (instance is not null)");
                        f.BeginBlock();
                        {
                            f.BeginTryCatch();
                            {
                                DependencyResolveGenerator.GenerateSetInjectionReady(
                                    f,
                                    memberName,
                                    memberType
                                );
                                if (member.HasReadyCallback)
                                {
                                    f.AppendLine(
                                        $"{NamingHelper.GetReadyCallbackMethodName(memberName)}();"
                                    );
                                }
                            }
                            f.CatchBlock("ex");
                            {
                                f.AppendLine(
                                    $"PrintError(ex.Message, \"{memberName}\", \"{member.MemberType.Name}\");"
                                );
                            }
                            f.EndTryCatch();
                        }
                        f.EndBlock();
                        f.AppendLine("else");
                        f.BeginBlock();
                        {
                            if (member.HasFailureCallback)
                            {
                                f.AppendLine(
                                    $"{NamingHelper.GetFailureCallbackMethodName(memberName)}();"
                                );
                            }
                        }
                        f.EndBlock();
                        f.AppendLine();

                        // 通知 TCS 注入结果；WaitForPhase 通过 ContinueWith 等待此 TCS
                        f.AppendLine($"{tcsName}.TrySetResult(instance is not null);");

                        if (validatedType.ImplementsIDependenciesResolved)
                        {
                            DependencyResolveGenerator.GenerateResolvedCallback(f, memberType);
                        }
                    }
                    f.EndBlock(",");
                    f.AppendLine($"requestorType: \"{validatedType.Symbol.Name}\"");
                }
                f.EndLevel();
                f.AppendLine(");");
            }

            if (injectMembersList.Length > 0)
            {
                f.AppendLine();
                f.AppendLine("return;");
                f.AppendLine();

                // PrintError 本地函数
                f.AppendLine("void PrintError(string exMsg, string memberName, string memberType)");
                f.BeginBlock();
                {
                    f.BeginStringBuilderAppend("errorMessage", true);
                    {
                        f.StringBuilderAppendLine(GeneratedStrings.ErrInjectionAssignFailed);
                        f.StringBuilderAppendLine($"  Type: {validatedType.Symbol.Name}");
                        f.StringBuilderAppendLine("  Member: {memberName}");
                        f.StringBuilderAppendLine("  Member Type: {memberType}");
                        f.StringBuilderAppendLine("  Exception: {exMsg}");
                    }
                    f.EndStringBuilderAppend();
                    f.AppendLine();
                    f.PrintError("errorMessage.ToString()");
                }
                f.EndBlock();
            }
        }
        f.EndBlock();
    }

    private static void GenerateIDependenciesResolvedSpecific(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        if (injectMembers.IsEmpty)
            return;

        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Type}> _unresolvedDependencies = new()"
        );
        f.BeginBlock();
        {
            foreach (var member in injectMembers)
                f.AppendLine($"typeof({member.MemberType.ToFullyQualifiedName()}),");
        }
        f.EndBlock(";");
        f.AppendLine();

        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void OnDependencyResolved<T>()");
        f.BeginBlock();
        {
            f.AppendLine("_unresolvedDependencies.Remove(typeof(T));");
            f.AppendLine("if (_unresolvedDependencies.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"(({GlobalNames.IDependenciesResolved})this).OnDependenciesResolved(IsAllDependenciesReady);"
                );
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine();
    }
}
