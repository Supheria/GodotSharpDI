using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// Generate WaitFor dependency waiting code
/// </summary>
internal static class WaitForPhase
{
    /// <summary>
    /// Generate WaitFor waiting code for a single Provide member.
    /// Called in ProvideServices() method body, generates code to register lambda to each dependency callback list.
    /// When all dependencies are ready (or failed), directly calls OnXxxWaitForResolved() on the main thread.
    /// </summary>
    public static void GenerateForMember(
        CodeFormatter f,
        MemberInfo provideMember,
        ImmutableArray<MemberInfo> allMembers,
        string scopeField = GlobalNames.LocalScope,
        Action? onAllResolved = null
    )
    {
        var waitForDeps = provideMember.WaitFor;

        if (waitForDeps.IsEmpty)
        {
            onAllResolved?.Invoke();
            return;
        }

        var memberName = provideMember.Symbol.Name;
        var pascalName = NamingHelper.ToPascalCase(memberName);
        var resolvedCallbackName = $"On{pascalName}WaitForResolved";

        f.AppendLine($"// WaitFor deps for {memberName}: {string.Join(", ", waitForDeps)}");

        // Use WaitForCoordinator from runtime library
        var coordinatorVar = $"__waitFor_{memberName}";
        f.AppendLine(
            $"var {coordinatorVar} = new {GlobalNames.WaitForCoordinator}("
                + $"{waitForDeps.Length}, {resolvedCallbackName});");
        f.AppendLine();

        foreach (var depName in waitForDeps)
        {
            var depMember = allMembers.FirstOrDefault(m => m.Symbol.Name == depName);
            if (depMember == null)
            {
                f.AppendLine($"// Error: WaitFor field '{depName}' not found in members");
                continue;
            }

            var listName = NamingHelper.GetInjectionCallbackListName(depName);

            f.AppendLine(
                $"{coordinatorVar}.Register({listName}, \"{depName}\", \"{memberName}\",");
            f.BeginLevel();
            {
                f.AppendLine($"{GlobalNames.ErrorReporter}.ErrorOutput,");
                f.AppendLine($"action => {GlobalNames.GodotCallable}.From(action).CallDeferred());");
            }
            f.EndLevel();
            f.AppendLine();
        }
    }

    /// <summary>
    /// Generate local function definition for WaitFor callback.
    /// </summary>
    public static void GenerateLocalFunction(
        CodeFormatter f,
        MemberInfo provideMember,
        Action onAllResolved
    )
    {
        var memberName = provideMember.Symbol.Name;
        var pascalName = NamingHelper.ToPascalCase(memberName);
        var resolvedCallbackName = $"On{pascalName}WaitForResolved";

        f.AppendLine($"async {GlobalNames.Task} {resolvedCallbackName}()");
        f.BeginBlock();
        {
            f.AppendLine($"// All WaitFor deps for '{memberName}' have settled");
            f.AppendLine();
            onAllResolved();
        }
        f.EndBlock();
        f.AppendLine();
    }
}
