using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Node 类型的 DI 代码生成器
/// </summary>
internal static class NodeDIGenerator
{
    /// <summary>
    /// 生成基础 DI 文件（Node 生命周期管理）
    /// </summary>
    public static void GenerateBaseDI(SourceProductionContext context, TypeNode node)
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidateTypeInfo, out var className);
        {
            GenerateNodeDICode(f, node.ValidateTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.g.cs", f.ToString());
    }

    private static void GenerateNodeDICode(CodeFormatter f, ValidateTypeInfo validateType)
    {
        GenerateServiceScopeField(f);
        f.AppendLine();

        GenerateGetServiceScope(f, validateType);
        f.AppendLine();

        GenerateAttachToScope(f, validateType);
        f.AppendLine();

        GenerateUnattachToScope(f, validateType);
        f.AppendLine();

        GenerateNotification(f, validateType);
    }

    private static void GenerateServiceScopeField(CodeFormatter f)
    {
        f.AppendLine($"private {GlobalNames.IScope}? _serviceScope;");
    }

    private static void GenerateGetServiceScope(CodeFormatter f, ValidateTypeInfo validateType)
    {
        // GetServiceScope
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.IScope}? GetServiceScope()");
        f.BeginBlock();
        {
            f.AppendLine("if (_serviceScope is not null)");
            f.BeginBlock();
            {
                f.AppendLine("return _serviceScope;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("var parent = GetParent();");
            f.AppendLine("while (parent is not null)");
            f.BeginBlock();
            {
                f.AppendLine($"if (parent is {GlobalNames.IScope} scope)");
                f.BeginBlock();
                {
                    f.AppendLine("_serviceScope = scope;");
                    f.AppendLine("return _serviceScope;");
                }
                f.EndBlock();
                f.AppendLine("parent = parent.GetParent();");
            }
            f.EndBlock();

            f.AppendLine(
                $"{GlobalNames.GodotGD}.PushError(\"{validateType.Symbol.Name} 没有最近的 Service Scope\");"
            );
            f.AppendLine("return null;");
        }
        f.EndBlock();
    }

    private static void GenerateAttachToScope(CodeFormatter f, ValidateTypeInfo validateType)
    {
        // AttachToScope
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void AttachToScope()");
        f.BeginBlock();
        {
            f.AppendLine("var scope = GetServiceScope();");
            f.AppendLine("if (scope is null) return;");

            if (validateType.Role == TypeRole.Host || validateType.Role == TypeRole.HostAndUser)
            {
                f.AppendLine("AttachHostServices(scope);");
            }

            if (validateType.Role == TypeRole.User || validateType.Role == TypeRole.HostAndUser)
            {
                f.AppendLine("ResolveUserDependencies(scope);");
            }
        }
        f.EndBlock();
    }

    private static void GenerateUnattachToScope(CodeFormatter f, ValidateTypeInfo validateType)
    {
        // UnattachToScope
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void UnattachToScope()");
        f.BeginBlock();
        {
            f.AppendLine("var scope = GetServiceScope();");
            f.AppendLine("if (scope is null) return;");

            if (validateType.Role == TypeRole.Host || validateType.Role == TypeRole.HostAndUser)
            {
                f.AppendLine("UnattachHostServices(scope);");
            }
        }
        f.EndBlock();
    }

    private static void GenerateNotification(CodeFormatter f, ValidateTypeInfo validateType)
    {
        // _Notification
        f.AppendLine("public override void _Notification(int what)");
        f.BeginBlock();
        {
            f.AppendLine("base._Notification(what);");
            f.AppendLine("switch ((long)what)");
            f.BeginBlock();
            {
                f.AppendLine("case NotificationEnterTree:");
                f.BeginBlock();
                {
                    f.AppendLine("AttachToScope();");
                    f.AppendLine("break;");
                }
                f.EndBlock();

                f.AppendLine("case NotificationExitTree:");
                f.BeginBlock();
                {
                    f.AppendLine("UnattachToScope();");
                    f.AppendLine("_serviceScope = null;");
                    f.AppendLine("break;");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }
}
