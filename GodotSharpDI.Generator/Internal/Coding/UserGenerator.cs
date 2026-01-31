using System.Linq;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.Helpers;
using GodotSharpDI.Generator.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharpDI.Generator.Internal.Data.TypeInfo;

namespace GodotSharpDI.Generator.Internal.Coding;

/// <summary>
/// User 代码生成器
/// </summary>
internal static class UserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成基础 DI 文件
        NodeDIGenerator.GenerateBaseDI(context, node);

        // 生成 User 特定代码
        GenerateUserSpecific(context, node);
    }

    /// <summary>
    /// 生成 User 特定代码（ResolveUserDependencies）
    /// </summary>
    public static void GenerateUserSpecific(SourceProductionContext context, TypeNode node)
    {
        // 收集 Inject 成员
        var injectMembers = node.TypeInfo.Members.Where(m => m.IsInjectMember).ToArray();

        // 如果没有 Inject 成员，不生成 User 代码
        if (injectMembers.Length == 0)
            return;

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.TypeInfo, out var className);
        {
            var implementsIServicesReady = node.TypeInfo.ImplementsIServicesReady;

            // 如果实现了 IServicesReady，生成依赖跟踪代码
            if (implementsIServicesReady && injectMembers.Length > 0)
            {
                GenerateDependencyTracking(f, injectMembers);
                f.AppendLine();
            }

            // 生成 ResolveUserDependencies
            GenerateResolveUserDependencies(f, injectMembers, implementsIServicesReady);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.User.g.cs", f.ToString());
    }

    private static void GenerateDependencyTracking(CodeFormatter f, MemberInfo[] injectMembersList)
    {
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
        MemberInfo[] injectMembersList,
        bool implementsIServicesReady
    )
    {
        // ResolveUserDependencies
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private void ResolveUserDependencies({GlobalNames.IScope} scope)");
        f.BeginBlock();
        {
            // 先注入 [Inject] 成员
            foreach (var member in injectMembersList)
            {
                f.AppendLine(
                    $"scope.ResolveDependency<{member.MemberType.ToFullyQualifiedName()}>(dependency =>"
                );
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine($"{member.Symbol.Name} = dependency;");
                        if (implementsIServicesReady)
                        {
                            f.AppendLine(
                                $"OnDependencyResolved<{member.MemberType.ToFullyQualifiedName()}>();"
                            );
                        }
                    }
                    f.EndTryCatch();
                }
                f.EndBlock(");");
            }
        }
        f.EndBlock();
    }
}
