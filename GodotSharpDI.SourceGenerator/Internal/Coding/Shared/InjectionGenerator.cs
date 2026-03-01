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
            // __lifetime_cancellation_tokens 字段无论是否有 Inject 成员都需要生成
            // 异步 Provider 依赖此 CancellationTokenSource
            GenerateLifetimeCancellationTokens(f);
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
                f.AppendLine();

                // 回调列表字段由 WaitForPhase 注册，由 ResolveDependencies 触发
                GenerateInjectionCallbackListFields(f, injectMembers);

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
    /// 生成 __lifetime_cancellation_tokens 字段。
    /// 在 ExitTree/EnterTree 时 Cancel 并重建，令所有飞行中的异步 Provider 自动中止。
    /// </summary>
    public static void GenerateLifetimeCancellationTokens(CodeFormatter f)
    {
        f.AppendHiddenMemberCommentAndAttribute(
            "CancellationTokenSource for async providers – cancelled and recreated on ExitTree/EnterTree"
        );
        f.AppendLine(
            "private global::System.Threading.CancellationTokenSource __lifetime_cancellation_tokens = new();"
        );
    }

    /// <summary>
    /// 生成注入回调列表字段。
    /// 类型为 List&lt;Action&lt;bool&gt;&gt;：true = 注入成功，false = 注入失败。
    /// WaitFor 机制直接向列表注册回调，当注入完成时在主线程上同步调用，
    /// 不再需要 ContinueWith / CallDeferred 跨线程跳转。
    /// </summary>
    public static void GenerateInjectionCallbackListFields(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var listName = NamingHelper.GetInjectionCallbackListName(member.Symbol.Name);
            f.AppendHiddenMemberCommentAndAttribute(
                $"WaitFor callback list for {member.Symbol.Name} (true=success, false=failure)"
            );
            f.AppendLine(
                $"private readonly {GlobalNames.List}<{GlobalNames.Action}<{GlobalNames.Bool}>>"
                    + $" {listName} = new();"
            );
            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成 ResetInjectionState() 方法。
    /// 在 EnterTree / ExitTree 时调用：
    ///   1. 取消并重建 __lifetime_cancellation_tokens，使所有飞行中的异步 Provider 收到 OperationCanceledException
    ///   2. 清空所有注入回调列表，丢弃已注册但尚未触发的 WaitFor 回调
    ///   3. 重置 ready 标识
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
            // 取消并重建 CTS，令所有飞行中的异步 Provider 自动中止
            f.AppendLine("__lifetime_cancellation_tokens.Cancel();");
            f.AppendLine("__lifetime_cancellation_tokens.Dispose();");
            f.AppendLine(
                "// Create a fresh token so any new async providers after EnterTree can run normally"
            );
            f.AppendLine(
                "__lifetime_cancellation_tokens = new global::System.Threading.CancellationTokenSource();"
            );

            if (!injectMembers.IsEmpty)
            {
                f.AppendLine();
                f.AppendLine(
                    "// Clear callback lists – discards all pending WaitFor registrations."
                );
                f.AppendLine("// Since all DI callbacks run on the main thread, there are no");
                f.AppendLine("// in-flight callbacks to wait for; Clear() is sufficient.");
            }

            foreach (var member in injectMembers)
            {
                var listName = NamingHelper.GetInjectionCallbackListName(member.Symbol.Name);
                var readyField = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                f.AppendLine($"{listName}.Clear();");
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
                                var fieldName = NamingHelper.GetInjectionReadyFieldName(memberName);
                                f.AppendLine($"{memberName} ??= instance;");
                                f.AppendLine($"{fieldName} = true;");
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
                        if (member.HasFailureCallback)
                        {
                            f.AppendLine("else");
                            f.BeginBlock();
                            {
                                {
                                    f.AppendLine(
                                        $"{NamingHelper.GetFailureCallbackMethodName(memberName)}();"
                                    );
                                }
                            }
                            f.EndBlock();
                        }
                        f.AppendLine();

                        // 通知所有 WaitFor 回调：注入结果已就绪（全部在主线程上执行）
                        var listName = NamingHelper.GetInjectionCallbackListName(memberName);
                        f.AppendLine("var resolved = instance is not null;");
                        f.AppendLine($"foreach (var cb in {listName})");
                        f.BeginBlock();
                        {
                            f.AppendLine("cb.Invoke(resolved);");
                        }
                        f.EndBlock();
                        f.AppendLine($"{listName}.Clear();");

                        if (validatedType.ImplementsIDependenciesResolved)
                        {
                            f.AppendLine($"OnDependencyResolved<{memberType}>();");
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
