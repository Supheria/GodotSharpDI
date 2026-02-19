using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

internal static class InjectionGenerator
{
    /// <summary>
    /// 生成 User 特定代码（ResolveUserDependencies）
    /// </summary>
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 收集 Inject 成员
        var injectMembers = node
            .ValidatedTypeInfo.Members.Where(m => m.IsInjectMember)
            .ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
        {
            // 如果有 Inject 成员
            if (!injectMembers.IsEmpty)
            {
                // 如果有带 FailureCallback 的成员，生成 partial 方法声明
                var membersWithFailureCallback = injectMembers.Where(m => m.HasFailureCallback).ToArray();
                if (membersWithFailureCallback.Length > 0)
                {
                    GenerateFailureCallbackDeclarations(f, membersWithFailureCallback);
                    f.AppendLine();
                }

                // 如果有带 ReadyCallback 的成员，生成 partial 方法声明
                var membersWithReadyCallback = injectMembers.Where(m => m.HasReadyCallback).ToArray();
                if (membersWithReadyCallback.Length > 0)
                {
                    GenerateReadyCallbackDeclarations(f, membersWithReadyCallback);
                    f.AppendLine();
                }

                GenerateInjectionReadyProperties(f, injectMembers);
                GenerateIsAllDependenciesReadyProperty(f, injectMembers);

                // 如果实现了 IDependenciesResolved，生成依赖跟踪代码
                if (node.ValidatedTypeInfo.ImplementsIDependenciesResolved)
                {
                    GenerateIDependenciesResolvedSpecific(f, injectMembers);
                }
            }

            // 生成 ResolveDependencies
            GenerateResolveDependencies(f, node.ValidatedTypeInfo, injectMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Inject.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成注入准备标识符字段 (IsXxxInjectionReady)
    /// </summary>
    public static void GenerateInjectionReadyProperties(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Whether member {member.Symbol.Name} has been successfully injected</summary>"
            );
            f.AppendLine($"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]");
            f.AppendLine($"private {GlobalNames.Bool} {fieldName} {{ get; set; }} = false;");
            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成 IsAllDependenciesReady 属性
    /// </summary>
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

        var fAttribute = f.CreateFromCurrentLevel();
        var fValue = f.CreateFromCurrentLevel();
        fValue.BeginLevel();
        {
            for (int i = 0; i < injectMembers.Length; i++)
            {
                var member = injectMembers[i];
                fAttribute.AppendLine(
                    $"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]"
                );
                var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                if (i > 0)
                {
                    fValue.AppendLine();
                    fValue.AppendRaw($"&& {fieldName} == true", true);
                }
                else
                {
                    fValue.AppendRaw($"{fieldName} == true", true);
                }
            }
            fValue.AppendRaw(";");
        }
        fValue.EndLevel();
        f.AppendLine("/// <summary>Whether all Inject members have been successfully injected</summary>");
        f.AppendRaw(fAttribute.ToString());
        f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady =>");
        f.AppendRaw(fValue.ToString());
        f.AppendLine();
    }

    private static void GenerateFailureCallbackDeclarations(
        CodeFormatter f,
        MemberInfo[] injectMembers
    )
    {
        // OnXxxInjectionFailed
        foreach (var member in injectMembers)
        {
            var methodName = NamingHelper.GetFailureCallbackMethodName(member.Symbol.Name);
            f.AppendLine($"/// <summary>Callback when injection of member {member.Symbol.Name} fails</summary>");
            f.AppendLine($"partial void {methodName}({GlobalNames.String} error);");
            f.AppendLine();
        }
    }

    private static void GenerateReadyCallbackDeclarations(
        CodeFormatter f,
        MemberInfo[] injectMembers
    )
    {
        // OnXxxInjectionReady
        foreach (var member in injectMembers)
        {
            var methodName = NamingHelper.GetReadyCallbackMethodName(member.Symbol.Name);
            f.AppendLine($"/// <summary>Callback when injection of member {member.Symbol.Name} succeeds</summary>");
            f.AppendLine($"partial void {methodName}();");
            f.AppendLine();
        }
    }

    private static void GenerateResolveDependencies(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembersList
    )
    {
        // ResolveUserDependencies
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ResolveDependencies()");
        f.BeginBlock();
        {
            f.AppendLine($"var {GlobalNames.LocalScope} = GetParentScope();");
            f.AppendLine($"if ({GlobalNames.LocalScope} is null)");
            f.BeginBlock();
            {
                f.PrintError($"$\"[GodotSharpDI] {validatedType.Symbol.Name}: Cannot find parent Scope in scene tree.\"");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // P3: 为每个 Inject 成员生成 TCS，供 WaitForPhase 复用
            foreach (var m in injectMembersList)
            {
                var tcsName = NamingHelper.GetInjectionTcsName(m.Symbol.Name);
                var mType = m.MemberType.ToFullyQualifiedName();
                f.AppendLine(
                    $"var {tcsName} = new global::System.Threading.Tasks" +
                    $".TaskCompletionSource<{GlobalNames.ResolutionResult}>();");
            }
            f.AppendLine();

            // 注入 [Inject] 成员
            foreach (var member in injectMembersList)
            {
                var memberType = member.MemberType.ToFullyQualifiedName();
                var memberName = member.Symbol.Name;
                var tcsName = NamingHelper.GetInjectionTcsName(memberName);

                f.AppendLine($"{GlobalNames.LocalScope}.ResolveDependency<{memberType}>(");
                f.BeginLevel();
                {
                    f.AppendLine("(result) =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (result.IsSuccess)");
                        f.BeginBlock();
                        {
                            f.BeginTryCatch();
                            {
                                DependencyResolveGenerator.GenerateSetInjectionReady(
                                    f,
                                    memberName,
                                    memberType
                                );

                                // 如果有就绪回调，调用它
                                if (member.HasReadyCallback)
                                {
                                    var callbackMethodName = NamingHelper.GetReadyCallbackMethodName(
                                        member.Symbol.Name
                                    );
                                    f.AppendLine($"{callbackMethodName}();");
                                }
                            }
                            f.CatchBlock("ex");
                            {
                                f.AppendLine(
                                    $"PushError(ex.Message, \"{member.Symbol.Name}\", \"{member.MemberType.Name}\");"
                                );
                            }
                            f.EndTryCatch();
                        }
                        f.EndBlock();
                        f.AppendLine("else");
                        f.BeginBlock();
                        {
                            // 如果有失败回调，调用它
                            if (member.HasFailureCallback)
                            {
                                var callbackMethodName = NamingHelper.GetFailureCallbackMethodName(
                                    member.Symbol.Name
                                );
                                f.AppendLine(
                                    $"{callbackMethodName}(result.ErrorMessage ?? \"Unknown error\");"
                                );
                            }
                        }
                        f.EndBlock();

                        // P3: 完成 TCS，让 WaitForPhase 可以通过 TCS.Task 等待注入结果
                        f.AppendLine($"{tcsName}.TrySetResult(result);");

                        // 如果实现了 IDependenciesResolved,调用跟踪方法
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

            f.AppendLine();
            f.AppendLine("return;");
            f.AppendLine();

            // PushError
            f.AppendLine("void PushError(string exMsg, string memberName, string memberType)");
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
        f.EndBlock();
    }

    /// <summary>
    /// 仅生成 IDependenciesResolved 相关的字段和方法
    /// (便捷方法,一次性生成所有内容)
    /// </summary>
    private static void GenerateIDependenciesResolvedSpecific(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        if (injectMembers.IsEmpty)
        {
            return;
        }

        // _unresolvedDependencies
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Type}> _unresolvedDependencies = new()"
        );
        f.BeginBlock();
        {
            foreach (var member in injectMembers)
            {
                f.AppendLine($"typeof({member.MemberType.ToFullyQualifiedName()}),");
            }
        }
        f.EndBlock(";");
        f.AppendLine();

        // OnDependencyResolved
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
