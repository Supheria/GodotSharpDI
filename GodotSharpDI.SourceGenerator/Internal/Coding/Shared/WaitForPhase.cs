using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

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
        var remainingVarName = $"_{memberName}_remaining";
        var resolvedCallbackName = $"On{pascalName}WaitForResolved";

        f.AppendLine($"// WaitFor deps for {memberName}: {string.Join(", ", waitForDeps)}");

        // Local counter: all decremented on main thread, no Interlocked needed
        f.AppendLine($"var {remainingVarName} = {waitForDeps.Length};");
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

            f.AppendLine($"// WaitFor: register main-thread callback for '{depName}'");

            // Register lambda to callback list; ResolveDependencies() triggers directly on main thread
            f.AppendLine($"{listName}.Add(__ok =>");
            f.BeginBlock();
            {
                f.AppendLine("if (!__ok)");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"{GlobalNames.GodotGD}.PrintErr("
                            + $"$\"[GodotSharpDI] WaitFor: dependency '{depName}' for '{memberName}' failed\");"
                    );
                }
                f.EndBlock();
                // Decrement regardless of success or failure; trigger callback when zero (consistent with old design behavior)
                f.AppendLine($"if (--{remainingVarName} == 0)");
                f.BeginBlock();
                {
                    // OnXxxWaitForResolved() is an async local function, if it contains await internally,
                    // the continuation may complete on a thread pool thread. ContinueWith uses TaskScheduler.Default,
                    // so body also executes on thread pool thread.
                    // GD.PrintErr itself is thread-safe, but to maintain consistency with the rest of the project
                    // (all Godot API calls are on main thread), dispatch error logs back to Godot main thread
                    // via Callable.From().CallDeferred(), avoiding potential thread safety issues in future extensions.
                    f.AppendLine($"_ = {resolvedCallbackName}().ContinueWith(t =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (t.IsFaulted)");
                        f.BeginBlock();
                        {
                            // Capture error message to local variable (ContinueWith body is on thread pool,
                            // cannot directly access Godot objects other than t)
                            f.AppendLine("var __errMsg = t.Exception?.GetBaseException().Message;");
                            f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                            f.BeginBlock();
                            {
                                f.AppendLine(
                                    $"{GlobalNames.GodotGD}.PrintErr("
                                        + $"$\"[GodotSharpDI] WaitFor callback '{resolvedCallbackName}' threw: {{__errMsg}}\");"
                                );
                            }
                            f.EndBlock(").CallDeferred();");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock(", global::System.Threading.Tasks.TaskScheduler.Default);");
                }
                f.EndBlock();
            }
            f.EndBlock(");");
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
