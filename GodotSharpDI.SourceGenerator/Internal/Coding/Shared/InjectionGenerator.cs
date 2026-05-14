using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

internal static class InjectionGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        var injectMembers = node
            .ValidatedTypeInfo.Members.Where(m => m.IsInjectMember)
            .ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
        {
            // __lifetime_cancellation_tokens field needs to be generated regardless of whether there are Inject members
            // Async Provider depends on this CancellationTokenSource
            GenerateLifetimeCancellationTokens(f);
            f.AppendLine();

            if (!injectMembers.IsEmpty)
            {
                var membersWithFailureCallback = injectMembers
                    .Where(m => m.HasFailureCallback)
                    .ToArray();
                if (membersWithFailureCallback.Length > 0)
                {
                    GenerateFailureCallbackDeclarations(f, membersWithFailureCallback);
                    f.AppendLine();
                }

                var membersWithReadyCallback = injectMembers
                    .Where(m => m.HasReadyCallback)
                    .ToArray();
                if (membersWithReadyCallback.Length > 0)
                {
                    GenerateReadyCallbackDeclarations(f, membersWithReadyCallback);
                    f.AppendLine();
                }

                GenerateInjectionReadyProperties(f, injectMembers);
                GenerateIsAllDependenciesReadyProperty(f, injectMembers);
                f.AppendLine();

                // Callback list fields are registered by WaitForPhase, triggered by ResolveDependencies
                GenerateInjectionCallbackListFields(f, injectMembers);

                if (node.ValidatedTypeInfo.ImplementsIDependenciesResolved)
                    GenerateIDependenciesResolvedSpecific(f, injectMembers);
            }

            GenerateResetInjectionState(f, injectMembers);
            GenerateResolveDependencies(f, node.ValidatedTypeInfo, injectMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Inject.g.cs", f.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // Public methods (called by other Generators)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate __lifetime_cancellation_tokens field.
    /// Cancel and recreate on ExitTree/EnterTree to automatically abort all in-flight async Providers.
    /// </summary>
    public static void GenerateLifetimeCancellationTokens(CodeFormatter f)
    {
        f.AppendHiddenMemberCommentAndAttribute(
            "CancellationTokenSource for async providers – cancelled and recreated on ExitTree/EnterTree"
        );
        f.AppendLine(
            "private global::System.Threading.CancellationTokenSource __lifetime_cancellation_tokens = new();"
        );
    }

    /// <summary>
    /// Generate injection callback list fields.
    /// Type is List&lt;Action&lt;bool&gt;&gt;: true = injection success, false = injection failure.
    /// WaitFor mechanism directly registers callbacks to the list, synchronously called on main thread when injection completes,
    /// no longer needs ContinueWith / CallDeferred cross-thread jumping.
    /// </summary>
    public static void GenerateInjectionCallbackListFields(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var listName = NamingHelper.GetInjectionCallbackListName(member.Symbol.Name);
            f.AppendHiddenMemberCommentAndAttribute(
                $"WaitFor callback list for {member.Symbol.Name} (true=success, false=failure)"
            );
            f.AppendLine(
                $"private readonly {GlobalNames.List}<{GlobalNames.Action}<{GlobalNames.Bool}>>"
                    + $" {listName} = new();"
            );
            f.AppendLine();
        }
    }

    /// <summary>
    /// Generate ResetInjectionState() method.
    /// Called on EnterTree / ExitTree:
    ///   1. Cancel and recreate __lifetime_cancellation_tokens, causing all in-flight async Providers to receive OperationCanceledException
    ///   2. Clear all injection callback lists, discarding registered but not yet triggered WaitFor callbacks
    ///   3. Reset ready flags
    /// </summary>
    public static void GenerateResetInjectionState(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "Reset injection state on EnterTree/ExitTree to cancel in-flight async operations"
        );
        f.AppendLine("private void ResetInjectionState()");
        f.BeginBlock();
        {
            // Cancel and recreate CTS to automatically abort all in-flight async Providers
            f.AppendLine("__lifetime_cancellation_tokens.Cancel();");
            f.AppendLine("__lifetime_cancellation_tokens.Dispose();");
            f.AppendLine(
                "// Create a fresh token so any new async providers after EnterTree can run normally"
            );
            f.AppendLine(
                "__lifetime_cancellation_tokens = new global::System.Threading.CancellationTokenSource();"
            );

            if (!injectMembers.IsEmpty)
            {
                f.AppendLine();
                f.AppendLine(
                    "// Clear callback lists – discards all pending WaitFor registrations."
                );
                f.AppendLine("// Since all DI callbacks run on the main thread, there are no");
                f.AppendLine("// in-flight callbacks to wait for; Clear() is sufficient.");
            }

            foreach (var member in injectMembers)
            {
                var listName = NamingHelper.GetInjectionCallbackListName(member.Symbol.Name);
                var readyField = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                f.AppendLine($"{listName}.Clear();");
                f.AppendLine($"{readyField} = false;");
                f.AppendLine($"{member.Symbol.Name} = default;");
            }
        }
        f.EndBlock();
        f.AppendLine();
    }

    public static void GenerateInjectionReadyProperties(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Whether {member.Symbol.Name} has been successfully injected</summary>"
            );
            f.AppendLine($"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]");
            f.AppendLine($"private {GlobalNames.Bool} {fieldName} {{ get; set; }} = false;");
            f.AppendLine();
        }
    }

    public static void GenerateIsAllDependenciesReadyProperty(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        if (injectMembers.IsEmpty)
        {
            f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady => true;");
            return;
        }

        var fAttr = f.CreateFromCurrentLevel();
        var fVal = f.CreateFromCurrentLevel();
        fVal.BeginLevel();
        {
            for (int i = 0; i < injectMembers.Length; i++)
            {
                var member = injectMembers[i];
                fAttr.AppendLine(
                    $"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]"
                );
                var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                if (i > 0)
                {
                    fVal.AppendLine();
                    fVal.AppendRaw($"&& {fieldName}", true);
                }
                else
                {
                    fVal.AppendRaw($"{fieldName}", true);
                }
            }
            fVal.AppendRaw(";");
        }
        fVal.EndLevel();

        f.AppendLine(
            "/// <summary>Whether all Inject members have been successfully injected</summary>"
        );
        f.AppendRaw(fAttr.ToString());
        f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady =>");
        f.AppendRaw(fVal.ToString());
        f.AppendLine();
    }

    // ──────────────────────────────────────────────────────────────
    // Private methods
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate partial method declaration for FailureCallback.
    /// </summary>
    private static void GenerateFailureCallbackDeclarations(CodeFormatter f, MemberInfo[] members)
    {
        foreach (var member in members)
        {
            var methodName = NamingHelper.GetFailureCallbackMethodName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Called when injection of {member.Symbol.Name} fails</summary>"
            );
            f.AppendLine($"partial void {methodName}();");
            f.AppendLine();
        }
    }

    private static void GenerateReadyCallbackDeclarations(CodeFormatter f, MemberInfo[] members)
    {
        foreach (var member in members)
        {
            var methodName = NamingHelper.GetReadyCallbackMethodName(member.Symbol.Name);
            var memberType = member.MemberType.ToFullyQualifiedName();
            // Parameter name: camelCase with leading underscores removed, first letter lowercase
            var paramName = NamingHelper.ToParameterName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>Called when injection of {member.Symbol.Name} succeeds. The parameter provides a non-null reference to the injected value.</summary>"
            );
            f.AppendLine($"partial void {methodName}({memberType} {paramName});");
            f.AppendLine();
        }
    }

    /// <summary>
    /// Generate ResolveDependencies() method.
    ///
    /// Call scope.ResolveDependency&lt;T&gt;(instance =&gt; { ... }) for each Inject member.
    /// Callback parameter "instance" is of type TExposed?:
    ///   null     → resolution failed
    ///   non-null → resolution succeeded, value is the actual service instance
    /// </summary>
    private static void GenerateResolveDependencies(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembersList
    )
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ResolveDependencies()");
        f.BeginBlock();
        {
            f.AppendLine($"var {GlobalNames.LocalScope} = GetParentScope();");
            f.AppendLine($"if ({GlobalNames.LocalScope} is null)");
            f.BeginBlock();
            {
                f.PrintError(
                    $"$\"[GodotSharpDI] {validatedType.Symbol.Name}: Cannot find parent Scope in scene tree.\""
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            foreach (var member in injectMembersList)
            {
                var memberType = member.MemberType.ToFullyQualifiedName();
                var memberName = member.Symbol.Name;
                f.AppendLine($"{GlobalNames.LocalScope}.ResolveDependency<{memberType}>(");
                f.BeginLevel();
                {
                    // Parameter name "instance" corresponds to DependencyResolveGenerator.GenerateSetInjectionReady
                    f.AppendLine("instance =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (instance is not null)");
                        f.BeginBlock();
                        {
                            f.BeginTryCatch();
                            {
                                var fieldName = NamingHelper.GetInjectionReadyFieldName(memberName);
                                f.AppendLine($"{memberName} ??= instance;");
                                f.AppendLine($"{fieldName} = true;");
                                if (member.HasReadyCallback)
                                {
                                    f.AppendLine(
                                        $"{NamingHelper.GetReadyCallbackMethodName(memberName)}(instance);"
                                    );
                                }
                            }
                            f.CatchBlock("ex");
                            {
                                f.AppendLine(
                                    $"PrintError(ex.Message, \"{memberName}\", \"{member.MemberType.Name}\");"
                                );
                            }
                            f.EndTryCatch();
                        }
                        f.EndBlock();
                        if (member.HasFailureCallback)
                        {
                            f.AppendLine("else");
                            f.BeginBlock();
                            {
                                {
                                    f.AppendLine(
                                        $"{NamingHelper.GetFailureCallbackMethodName(memberName)}();"
                                    );
                                }
                            }
                            f.EndBlock();
                        }
                        f.AppendLine();

                        // Notify all WaitFor callbacks: injection result is ready (all executed on main thread)
                        var listName = NamingHelper.GetInjectionCallbackListName(memberName);
                        f.AppendLine("var resolved = instance is not null;");
                        f.AppendLine($"foreach (var cb in {listName})");
                        f.BeginBlock();
                        {
                            f.AppendLine("cb.Invoke(resolved);");
                        }
                        f.EndBlock();
                        f.AppendLine($"{listName}.Clear();");

                        if (validatedType.ImplementsIDependenciesResolved)
                        {
                            f.AppendLine($"OnDependencyResolved<{memberType}>();");
                        }
                    }
                    f.EndBlock(",");
                    f.AppendLine($"requestorType: \"{validatedType.Symbol.Name}\"");
                }
                f.EndLevel();
                f.AppendLine(");");
            }

            if (injectMembersList.Length > 0)
            {
                f.AppendLine();
                f.AppendLine("return;");
                f.AppendLine();

                // PrintError local function
                f.AppendLine("void PrintError(string exMsg, string memberName, string memberType)");
                f.BeginBlock();
                {
                    f.BeginStringBuilderAppend("errorMessage", true);
                    {
                        f.StringBuilderAppendLine(GeneratedStrings.ErrInjectionAssignFailed);
                        f.StringBuilderAppendLine($"  Type: {validatedType.Symbol.Name}");
                        f.StringBuilderAppendLine("  Member: {memberName}");
                        f.StringBuilderAppendLine("  Member Type: {memberType}");
                        f.StringBuilderAppendLine("  Exception: {exMsg}");
                    }
                    f.EndStringBuilderAppend();
                    f.AppendLine();
                    f.PrintError("errorMessage.ToString()");
                }
                f.EndBlock();
            }
        }
        f.EndBlock();
    }

    private static void GenerateIDependenciesResolvedSpecific(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        if (injectMembers.IsEmpty)
            return;

        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Type}> _unresolvedDependencies = new()"
        );
        f.BeginBlock();
        {
            foreach (var member in injectMembers)
                f.AppendLine($"typeof({member.MemberType.ToFullyQualifiedName()}),");
        }
        f.EndBlock(";");
        f.AppendLine();

        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void OnDependencyResolved<T>()");
        f.BeginBlock();
        {
            f.AppendLine("_unresolvedDependencies.Remove(typeof(T));");
            f.AppendLine("if (_unresolvedDependencies.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"(({GlobalNames.IDependenciesResolved})this).OnDependenciesResolved();"
                );
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine();
    }
}
