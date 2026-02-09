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

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
        {
            GenerateProvideHostServices(f, node.ValidatedTypeInfo, singletonMembers);
            f.AppendLine();
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Host.g.cs", f.ToString());
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

                // 只调用一次 ProvideService（使用成员类型）
                f.BeginTryCatch();
                {
                    f.AppendLine($"scope.ProvideService<{memberType}>({memberName});");
                }
                f.CatchBlock("ex");
                {
                    f.AppendLine(
                        $"var errorMessage = $\"Host '{validatedType.Symbol.Name}' 无法提供服务。 成员 '{memberName}' 异常: {{ex.Message}}\";"
                    );
                    f.AppendLine($"scope.ProvideService<{memberType}>(null, errorMessage);");
                }
                f.EndTryCatch();
            }
            f.AppendLine();
        }
        f.EndBlock();
    }
}
