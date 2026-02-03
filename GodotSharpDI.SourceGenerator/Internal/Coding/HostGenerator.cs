using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Host 代码生成器
/// </summary>
internal static class HostGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成 Node 声明周期
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // 生成 Host 特定代码
        GenerateHostSpecific(context, node);
    }

    /// <summary>
    /// 生成 Host 特定代码（AttachHostServices/UnattachHostServices）
    /// </summary>
    public static void GenerateHostSpecific(SourceProductionContext context, TypeNode node)
    {
        // 收集 Singleton 成员
        var singletonMembers = node
            .ValidatedTypeInfo.Members.Where(m => m.IsSingletonMember)
            .ToArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            // AttachHostServices
            GenerateAttachHostServices(f, singletonMembers);
            f.AppendLine();

            // UnattachHostServices
            GenerateUnattachHostServices(f, singletonMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Host.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 AttachHostServices 方法
    /// </summary>
    private static void GenerateAttachHostServices(CodeFormatter f, MemberInfo[] singletonMembers)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void AttachHostServices()");
        f.BeginBlock();
        {
            f.AppendLine("var scope = GetParentScope();");
            f.AppendLine("if (scope is null) return;");
            f.AppendLine();

            foreach (var member in singletonMembers)
            {
                foreach (var exposedType in member.ExposedTypes)
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine(
                            $"scope.RegisterService<{exposedType.ToFullyQualifiedName()}>({member.Symbol.Name});"
                        );
                    }
                    f.EndTryCatch();
                }
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成 UnattachHostServices 方法
    /// </summary>
    private static void GenerateUnattachHostServices(CodeFormatter f, MemberInfo[] singletonMembers)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void UnattachHostServices()");
        f.BeginBlock();
        {
            f.AppendLine("var scope = GetParentScope();");
            f.AppendLine("if (scope is null) return;");
            f.AppendLine();

            foreach (var member in singletonMembers)
            {
                foreach (var exposedType in member.ExposedTypes)
                {
                    f.AppendLine(
                        $"scope.UnregisterService<{exposedType.ToFullyQualifiedName()}>();"
                    );
                }
            }
        }
        f.EndBlock();
    }
}
