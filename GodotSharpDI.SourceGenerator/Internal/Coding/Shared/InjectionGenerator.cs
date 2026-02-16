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
                $"/// <summary>成员 {member.Symbol.Name} 是否成功注入依赖的标识符</summary>"
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
        f.AppendLine("/// <summary>所有 Inject 成员是否都成功注入依赖的标识符</summary>");
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
            f.AppendLine($"/// <summary>成员 {member.Symbol.Name} 依赖注入失败时的回调</summary>");
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
            f.AppendLine($"/// <summary>成员 {member.Symbol.Name} 依赖注入成功时的回调</summary>");
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
            f.AppendLine("var scope = GetParentScope();");
            f.AppendLine("if (scope is null)");
            f.BeginBlock();
            {
                f.PrintError($"\"[GodotSharpDI] {validatedType.Symbol.Name} 找不到父 Scope\"");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // 注入 [Inject] 成员
            foreach (var member in injectMembersList)
            {
                var memberType = member.MemberType.ToFullyQualifiedName();
                var memberName = member.Symbol.Name;

                f.AppendLine($"scope.ResolveDependency<{memberType}>(");
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
                    f.StringBuilderAppendLine("[GodotSharpDI] 依赖赋值失败");
                    f.StringBuilderAppendLine($"  User 类型: {validatedType.Symbol.Name}");
                    f.StringBuilderAppendLine("  成员: {memberName}");
                    f.StringBuilderAppendLine("  成员类型: {memberType}");
                    f.StringBuilderAppendLine("  异常: {exMsg}");
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
