using System.Linq;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.Helpers;
using GodotSharpDI.Generator.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharpDI.Generator.Internal.Data.TypeInfo;

namespace GodotSharpDI.Generator.Internal.Coding;

/// <summary>
/// Host 代码生成器
/// </summary>
internal static class HostGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成基础 DI 文件
        NodeDIGenerator.GenerateBaseDI(context, node);

        // 生成 Host 特定代码
        GenerateHostSpecific(context, node);
    }

    /// <summary>
    /// 生成 Host 特定代码（AttachHostServices/UnattachHostServices）
    /// </summary>
    public static void GenerateHostSpecific(SourceProductionContext context, TypeNode node)
    {
        // 收集 Singleton 成员
        var singletonMembers = node.TypeInfo.Members.Where(m => m.IsSingletonMember).ToArray();

        // 如果没有 Singleton 成员，不生成 Host 代码
        if (singletonMembers.Length == 0)
            return;

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.TypeInfo, out var className);
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
        f.AppendLine($"private void AttachHostServices({GlobalNames.IScope} scope)");
        f.BeginBlock();
        {
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
        f.AppendLine($"private void UnattachHostServices({GlobalNames.IScope} scope)");
        f.BeginBlock();
        {
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
