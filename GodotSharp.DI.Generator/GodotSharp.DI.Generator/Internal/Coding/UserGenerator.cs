using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Coding;

/// <summary>
/// User 代码生成器
/// </summary>
internal static class UserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        var type = node.TypeInfo;
        var namespaceName = type.Symbol.ContainingNamespace.ToDisplayString();
        var className = type.Symbol.Name;

        // 生成基础 DI 文件
        NodeDIGenerator.GenerateBaseDI(context, type, namespaceName, className);

        // 生成 User 特定代码
        GenerateUserSpecific(context, type, namespaceName, className);
    }

    /// <summary>
    /// 生成 User 特定代码（ResolveUserDependencies）
    /// </summary>
    public static void GenerateUserSpecific(
        SourceProductionContext context,
        TypeInfo type,
        string namespaceName,
        string className
    )
    {
        // 收集 Inject 成员
        var injectMembers = type
            .Members.Where(m =>
                m.Kind == MemberKind.InjectField || m.Kind == MemberKind.InjectProperty
            )
            .ToArray();

        // 如果没有 Inject 成员，不生成 User 代码
        if (injectMembers.Length == 0)
            return;

        var f = new CodeFormatter();

        f.BeginClassDeclaration(namespaceName, className);
        {
            // 如果实现了 IServicesReady，生成依赖跟踪代码
            if (type.ImplementsIServicesReady && injectMembers.Length > 0)
            {
                GenerateDependencyTracking(f, injectMembers);
                f.AppendLine();
            }

            // 生成 ResolveUserDependencies
            GenerateResolveUserDependencies(f, type, injectMembers);
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
                f.AppendLine($"typeof({member.MemberType.ToDisplayString()}),");
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
        TypeInfo type,
        MemberInfo[] injectMembersList
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
                    $"scope.ResolveDependency<{member.MemberType.ToDisplayString()}>(dependency =>"
                );
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine($"{member.Symbol.Name} = dependency;");
                        if (type.ImplementsIServicesReady)
                        {
                            f.AppendLine(
                                $"OnDependencyResolved<{member.MemberType.ToDisplayString()}>();"
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
