using System;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// 生成 Node 生命周期管理
/// </summary>
internal static class NodeLifeCycleGenerator
{
    public static void Generate(
        SourceProductionContext context,
        ValidatedTypeInfo validatedTypeInfo
    )
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(validatedTypeInfo, out var className);
        {
            GenerateNodeDICode(f, validatedTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Lifecycle.g.cs", f.ToString());
    }

    private static void GenerateNodeDICode(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        GenerateParentScopeField(f);
        f.AppendLine();

        GenerateGetParentScope(f, validatedType);
        f.AppendLine();

        GenerateNotification(f, validatedType);
    }

    private static void GenerateParentScopeField(CodeFormatter f)
    {
        // _parentScope
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.IScope}? _parentScope;");
    }

    private static void GenerateGetParentScope(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // GetParentScope
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.IScope}? GetParentScope()");
        f.BeginBlock();
        {
            f.AppendLine("if (_parentScope is not null)");
            f.BeginBlock();
            {
                f.AppendLine("return _parentScope;");
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
                    f.AppendLine("_parentScope = scope;");
                    f.AppendLine("return _parentScope;");
                }
                f.EndBlock();
                f.AppendLine("parent = parent.GetParent();");
            }
            f.EndBlock();
            f.AppendLine("return null;");
        }
        f.EndBlock();
    }

    private static void GenerateNotification(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // _Notification
        f.AppendLine("public override partial void _Notification(int what)");
        f.BeginBlock();
        {
            f.AppendLine("base._Notification(what);");
            f.AppendLine("switch ((long)what)");
            f.BeginBlock();
            {
                // NotificationEnterTree
                f.AppendLine("case NotificationEnterTree:");
                f.BeginBlock();
                {
                    f.AppendLine("_parentScope = null;");
                    f.AppendLine("break;");
                }
                f.EndBlock();
                // NotificationReady
                f.AppendLine("case NotificationReady:");
                f.BeginBlock();
                {
                    switch (validatedType.Role)
                    {
                        case TypeRole.Host:
                            f.AppendLine("ProvideHostServices();");
                            break;
                        case TypeRole.User:
                            f.AppendLine("ResolveUserDependencies();");
                            break;
                        case TypeRole.HostAndUser:
                            f.AppendLine("ProvideHostServices();");
                            f.AppendLine("ResolveUserDependencies();");
                            break;
                        case TypeRole.Scope:
                            f.AppendLine("InstantiateScopeSingletons();");
                            f.AppendLine("StartDependencyMonitoring();");
                            break;
                    }
                    f.AppendLine("break;");
                }
                f.EndBlock();
                // NotificationExitTree
                f.AppendLine("case NotificationExitTree:");
                f.BeginBlock();
                {
                    f.AppendLine("_parentScope = null;");
                    f.AppendLine("break;");
                }
                f.EndBlock();
                // NotificationPredelete
                f.AppendLine("case NotificationPredelete:");
                f.BeginBlock();
                {
                    switch (validatedType.Role)
                    {
                        case TypeRole.Scope:
                            f.AppendLine("DisposeScopeSingletons();");
                            f.AppendLine("StopDependencyMonitoring();");
                            f.AppendLine("ReportUnresolvedDependencies();");
                            break;
                    }
                    f.AppendLine("break;");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }
}
