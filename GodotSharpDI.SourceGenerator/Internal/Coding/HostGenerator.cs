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
    /// 生成 Host 特定代码（ProvideHostServices/UnattachHostServices）
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
            GenerateProvideHostServices(f, singletonMembers);
            f.AppendLine();
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Host.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 ProvideHostServices 方法
    /// </summary>
    private static void GenerateProvideHostServices(CodeFormatter f, MemberInfo[] singletonMembers)
    {
        // ProvideHostServices
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ProvideHostServices()");
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
                            $"scope.ProvideService<{exposedType.ToFullyQualifiedName()}>({member.Symbol.Name});"
                        );
                    }
                    f.EndTryCatch();
                }
            }
        }
        f.EndBlock();
    }
}
