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
            GenerateProvideHostServices(f, node.ValidatedTypeInfo, singletonMembers);
            f.AppendLine();
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Host.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 ProvideHostServices 方法
    /// </summary>
    private static void GenerateProvideHostServices(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        MemberInfo[] singletonMembers
    )
    {
        // ProvideHostServices
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ProvideHostServices()");
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

            foreach (var member in singletonMembers)
            {
                var memberName = member.Symbol.Name;
                var memberType = member.MemberType.ToFullyQualifiedName();

                foreach (var exposedType in member.ExposedTypes)
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine(
                            $"scope.ProvideService<{exposedType.ToFullyQualifiedName()}>({memberName});"
                        );
                    }
                    f.CatchBlock("ex");
                    {
                        f.BeginStringBuilderAppend("errorMessage", true);
                        {
                            f.StringBuilderAppendLine($"[{ShortNames.GodotSharpDI}] 服务提供失败");
                            f.StringBuilderAppendLine($"  Host: {validatedType.Symbol.Name}");
                            f.StringBuilderAppendLine($"  成员: {memberName}");
                            f.StringBuilderAppendLine($"  类型: {memberType}");
                            f.StringBuilderAppendLine("  异常: {ex.Message}");
                        }
                        f.EndStringBuilderAppend();
                        f.AppendLine();

                        f.PushError("errorMessage.ToString()");
                    }
                    f.EndTryCatch();
                }
            }
        }
        f.EndBlock();
    }
}
