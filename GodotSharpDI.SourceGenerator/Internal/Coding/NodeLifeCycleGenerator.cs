using System;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Generate Node lifecycle management
/// </summary>
internal static class NodeLifeCycleGenerator
{
    public static void Generate(
        SourceProductionContext context,
        ValidatedTypeInfo validatedTypeInfo
    )
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(validatedTypeInfo, out var fileName);
        {
            GenerateNodeDICode(f, validatedTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Lifecycle.g.cs", f.ToString());
    }

    private static void GenerateNodeDICode(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        GenerateParentScopeField(f);
        f.AppendLine();

        GenerateGetParentScope(f, validatedType);
        f.AppendLine();

        GenerateNotification(f, validatedType);
    }

    private static void GenerateErrorReporterInit(CodeFormatter f)
    {
        f.AppendLine(
            $"{GlobalNames.ErrorReporter}.ErrorOutput = {GlobalNames.GodotGD}.PrintErr;",
            "Initialize ErrorReporter to use Godot error output");
    }

    private static void GenerateParentScopeField(CodeFormatter f)
    {
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.IScope}? __parentScope;");
    }

    private static void GenerateGetParentScope(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.IScope}? GetParentScope()");
        f.BeginBlock();
        {
            f.AppendLine("if (__parentScope is not null)");
            f.BeginBlock();
            {
                f.AppendLine("return __parentScope;");
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
                    f.AppendLine("__parentScope = scope;");
                    f.AppendLine("return __parentScope;");
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
                    GenerateErrorReporterInit(f);
                    f.AppendLine("__parentScope = null;");
                    if (validatedType.Role == TypeRole.Host || validatedType.Role == TypeRole.User)
                    {
                        // ResetInjectionState: Increment Generation + Reset TCS/ready flags
                        // Ensure a fresh injection state when re-entering the scene tree, old operation callbacks will be invalidated due to Generation mismatch
                        f.AppendLine("ResetInjectionState();");
                    }
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
                            f.AppendLine("ProvideServices();");
                            f.AppendLine("ResolveDependencies();");
                            break;
                        case TypeRole.User:
                            f.AppendLine("ResolveDependencies();");
                            break;
                        case TypeRole.Scope:
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
                    f.AppendLine("__parentScope = null;");
                    if (validatedType.Role == TypeRole.Host || validatedType.Role == TypeRole.User)
                    {
                        // FIX3: Immediately invalidate all in-flight async operations when node exits scene tree.
                        //       ResetInjectionState increments _diGeneration,
                        //       any queued ContinueWith / CallDeferred callbacks will find Generation mismatch and silently exit.
                        f.AppendLine("ResetInjectionState();");
                    }
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
