using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// Generate service provision code for [Provide] marked members
/// </summary>
internal static class ServiceProvisionPhase
{
    /// <summary>
    /// Generate service provision call statement for a single member.
    /// </summary>
    public static void GenerateMemberProvide(
        CodeFormatter f,
        MemberInfo member,
        string scopeField,
        string providerTypeName,
        string instancePrefix = "",
        bool inAsyncContext = false
    )
    {
        var memberAccess = GetMemberAccess(member, instancePrefix);
        var implType = member.MemberType.ToFullyQualifiedName();

        f.AppendLine($"// Provide service: {implType}");

        if (member.IsAsync)
        {
            if (inAsyncContext)
                f.AppendLine(
                    $"await ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField}, __lifetime_cancellation_tokens.Token);"
                );
            else
                f.AppendLine(
                    $"_ = ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField}, __lifetime_cancellation_tokens.Token);"
                );
        }
        else
        {
            GenerateSyncProvide(f, memberAccess, implType, scopeField, providerTypeName);
        }

        f.AppendLine();
    }

    /// <summary>
    /// Generate all async provider helper methods.
    /// </summary>
    public static void GenerateAsyncProviderMethods(
        CodeFormatter f,
        ImmutableArray<MemberInfo> asyncMembers,
        string providerTypeName
    )
    {
        foreach (var member in asyncMembers.Where(m => m.IsAsync))
            GenerateAsyncProviderMethod(f, member, providerTypeName);
    }

    /// <summary>
    /// Generate a single async provider method (instance method).
    /// </summary>
    private static void GenerateAsyncProviderMethod(
        CodeFormatter f,
        MemberInfo member,
        string providerTypeName
    )
    {
        var implType = member.MemberType.ToFullyQualifiedName();
        var taskTypeName = $"{GlobalNames.Task}<{implType}>";
        var methodName = $"ProvideAsync_{member.Symbol.Name}";

        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"private async {GlobalNames.Task} {methodName}("
                + $"{taskTypeName} task, {GlobalNames.IScope} scope, global::System.Threading.CancellationToken ct)"
        );
        f.BeginBlock();
        {
            // OperationCanceledException is caught before Exception to ensure silent exit on cancellation
            f.AppendLine("try");
            f.BeginBlock();
            {
                f.AppendLine("var result = await task;");
                f.AppendLine();
                // Check token after await returns (ExitTree has cancelled)
                f.AppendLine("ct.ThrowIfCancellationRequested();");
                f.AppendLine();
                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    // Token may be cancelled again during CallDeferred queuing, check once after entering
                    f.AppendLine("if (ct.IsCancellationRequested) return;");
                    f.AppendLine(
                        $"scope.ProvideService<{implType}>(result, \"{providerTypeName}\");"
                    );
                }
                f.EndBlock(").CallDeferred();");
            }
            f.EndBlock();
            f.AppendLine("catch (global::System.OperationCanceledException)");
            f.BeginBlock();
            {
                // Node exited scene tree, silent exit, do not call ProvideService
                f.AppendLine(
                    "// Node exited scene tree – silent cancellation, do not call ProvideService"
                );
            }
            f.EndBlock();
            f.AppendLine("catch (global::System.Exception ex)");
            f.BeginBlock();
            {
                f.AppendLine("if (ct.IsCancellationRequested) return;");
                f.AppendLine();
                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PrintErr("
                        + $"$\"[GodotSharpDI] Async provider for {implType} threw: {{ex.Message}}\");"
                );

                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    f.AppendLine("if (ct.IsCancellationRequested) return;");
                    f.AppendLine(
                        $"scope.ProvideService<{implType}>(null, \"{providerTypeName}\");"
                    );
                }
                f.EndBlock(").CallDeferred();");
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// Generate synchronous service provision code.
    ///   Success → scope.ProvideService&lt;T&gt;(instance, providerType)
    ///   Exception → scope.ProvideService&lt;T&gt;(null, providerType)
    /// </summary>
    private static void GenerateSyncProvide(
        CodeFormatter f,
        string memberAccess,
        string implType,
        string scopeField,
        string providerTypeName
    )
    {
        f.BeginTryCatch();
        {
            f.AppendLine($"var instance = {memberAccess};");
            f.AppendLine(
                $"{scopeField}.ProvideService<{implType}>(instance, \"{providerTypeName}\");"
            );
        }
        f.CatchBlock("ex");
        {
            f.AppendLine(
                $"{GlobalNames.GodotGD}.PrintErr("
                    + $"$\"[GodotSharpDI] Provider for {implType} threw: {{ex.Message}}\");"
            );
            f.AppendLine($"{scopeField}.ProvideService<{implType}>(null, \"{providerTypeName}\");");
        }
        f.EndTryCatch();
    }

    /// <summary>
    /// Get member access expression.
    /// </summary>
    private static string GetMemberAccess(MemberInfo member, string instancePrefix)
    {
        var prefix = string.IsNullOrEmpty(instancePrefix) ? "" : $"{instancePrefix}.";
        return member.Kind switch
        {
            MemberKind.ProvideField => $"{prefix}{member.Symbol.Name}",
            MemberKind.ProvideProperty => $"{prefix}{member.Symbol.Name}",
            MemberKind.ProvideMethod => $"{prefix}{member.Symbol.Name}()",
            _ => throw new ArgumentOutOfRangeException(nameof(member), member.Kind, "Unsupported Provide member kind"),
        };
    }
}
