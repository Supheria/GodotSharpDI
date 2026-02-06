using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// User 代码生成器
/// </summary>
internal static class UserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成基础 DI 文件
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // 生成 User 特定代码
        GenerateUserSpecific(context, node);
    }

    /// <summary>
    /// 生成 User 特定代码（ResolveUserDependencies）
    /// </summary>
    public static void GenerateUserSpecific(SourceProductionContext context, TypeNode node)
    {
        // 收集 Inject 成员
        var injectMembers = node.ValidatedTypeInfo.Members.Where(m => m.IsInjectMember).ToArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            var implementsIServicesReady = node.ValidatedTypeInfo.ImplementsIServicesReady;

            // 如果实现了 IServicesReady，生成依赖跟踪代码
            if (implementsIServicesReady && injectMembers.Length > 0)
            {
                GenerateDependencyTracking(f, injectMembers);
                f.AppendLine();
            }

            // 生成 ResolveUserDependencies
            GenerateResolveUserDependencies(f, node.ValidatedTypeInfo, injectMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.User.g.cs", f.ToString());
    }

    private static void GenerateDependencyTracking(CodeFormatter f, MemberInfo[] injectMembersList)
    {
        // _unresolvedDependencies
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Type}> _unresolvedDependencies = new()"
        );
        f.BeginBlock();
        {
            foreach (var member in injectMembersList)
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
                f.AppendLine($"(({GlobalNames.IServicesReady})this).OnServicesReady();");
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine();
    }

    private static void GenerateResolveUserDependencies(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        MemberInfo[] injectMembersList
    )
    {
        // ResolveUserDependencies
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ResolveUserDependencies()");
        f.BeginBlock();
        {
            f.AppendLine("var scope = GetParentScope();");
            f.AppendLine("if (scope is null)");
            f.BeginBlock();
            {
                f.PushError($"\"[GodotSharpDI] {validatedType.Symbol.Name} 找不到父 Scope\"");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // 注入 [Inject] 成员
            foreach (var member in injectMembersList)
            {
                var memberTypeName = member.MemberType.ToFullyQualifiedName();
                var memberName = member.Symbol.Name;
                f.AppendLine($"scope.ResolveDependency<{memberTypeName}>(");
                f.BeginLevel();
                {
                    f.AppendLine("dependency =>");
                    f.BeginBlock();
                    {
                        f.BeginTryCatch();
                        {
                            f.AppendLine($"{memberName} = dependency;");
                        }
                        f.CatchBlock("ex");
                        {
                            f.AppendLine(
                                $"PushError(ex.Message, \"{member.Symbol.Name}\", \"{member.MemberType.Name}\");"
                            );
                        }
                        f.EndTryCatch();
                        if (validatedType.ImplementsIServicesReady)
                        {
                            f.AppendLine($"OnDependencyResolved<{memberTypeName}>();");
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
                f.PushError("errorMessage.ToString()");
            }
            f.EndBlock();
        }
        f.EndBlock();
    }
}
