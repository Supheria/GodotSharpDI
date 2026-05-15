using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;

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
    /// Generate a single async provider method — delegates to AsyncProviderRunner.
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
            $"private {GlobalNames.Task} {methodName}("
                + $"{taskTypeName} task, {GlobalNames.IScope} scope, global::System.Threading.CancellationToken ct)"
        );
        f.BeginBlock();
        {
            f.AppendLine($"return {GlobalNames.AsyncProviderRunner}.Run(");
            f.BeginLevel();
            {
                f.AppendLine("task,");
                f.AppendLine($"(inst, pt) => scope.ProvideService<{implType}>(inst, pt),");
                f.AppendLine($"\"{providerTypeName}\",");
                f.AppendLine("ct,");
                f.AppendLine($"{GlobalNames.ErrorReporter}.ErrorOutput,");
                f.AppendLine($"action => {GlobalNames.GodotCallable}.From(action).CallDeferred());");
            }
            f.EndLevel();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// Generate synchronous service provision code — delegates to SyncProviderRunner.
    /// </summary>
    private static void GenerateSyncProvide(
        CodeFormatter f,
        string memberAccess,
        string implType,
        string scopeField,
        string providerTypeName
    )
    {
        f.AppendLine($"{GlobalNames.SyncProviderRunner}.Run<{implType}>(");
        f.BeginLevel();
        {
            f.AppendLine($"() => {memberAccess},");
            f.AppendLine($"(inst, pt) => {scopeField}.ProvideService<{implType}>(inst, pt),");
            f.AppendLine($"\"{providerTypeName}\");");
        }
        f.EndLevel();
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
