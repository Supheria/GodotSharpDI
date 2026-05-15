using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope interface implementation code generator
///
/// v1.3.0 refactoring: Removed ResolutionResult, IScope directly uses nullable types:
///   ProvideService&lt;TImpl&gt;(TImpl? instance)          — null means provision failed
///   ResolveDependency&lt;TExposed&gt;(Action&lt;TExposed?&gt;) — callback receiving null means resolution failed
/// </summary>
internal static class ScopeInterfaceGenerator
{
    public static void GenerateInterface(SourceProductionContext context, ScopeNode node)
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
        {
            GenerateProvideService(f);
            f.AppendLine();

            GenerateResolveDependency(f, node.ValidatedTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.IScope.g.cs", f.ToString());
    }

    // =========================================================
    // ProvideService<TImpl>(TImpl? instance)
    // =========================================================

    private static void GenerateProvideService(CodeFormatter f)
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "Provide service by implementation type. instance == null means service creation failed."
        );
        f.AppendLine(
            $"void {GlobalNames.IScope}.ProvideService<TImpl>(TImpl? instance, {GlobalNames.String} providerType)"
        );
        f.AppendTypeConstraints("where TImpl : class");
        f.BeginBlock();
        {
            f.AppendLine("var implType = typeof(TImpl);");
            f.AppendLine();

            // Find ServiceCache (key is implementation type)
            GenerateProvideServiceLookup(f);

            // Handle failure scenario (instance == null)
            f.AppendLine("if (instance is null)");
            f.BeginBlock();
            {
                GenerateProvideNullHandling(f);
            }
            f.EndBlock();
            f.AppendLine("else");
            f.BeginBlock();
            {
                GenerateProvideSuccess(f);
            }
            f.EndBlock();
            f.AppendLine();

            // Notify all waiters (key is implementation type)
            GenerateProvideWaiterNotification(f);
        }
        f.EndBlock();
    }

    private static void GenerateProvideServiceLookup(CodeFormatter f)
    {
        f.AppendLine("if (!ServiceCache.TryGetValue(implType, out var cacheEntry))");
        f.BeginBlock();
        {
            f.AppendLine("var parent = GetParentScope();", "Forward to parent Scope");
            f.AppendLine("if (parent is not null)");
            f.BeginBlock();
            {
                f.AppendLine("parent.ProvideService<TImpl>(instance, providerType);");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();
            f.PrintError(
                "$\"[GodotSharpDI] Host '{providerType}' cannot provide service"
                + "\\n  Reason: No Scope in scene tree contains implementation type: {implType.Name}\""
            );
            f.AppendLine("return;");
        }
        f.EndBlock();
        f.AppendLine();
    }

    private static void GenerateProvideNullHandling(CodeFormatter f)
    {
        f.AppendLine("// Failure scenario: service creation failed");
        f.AppendLine("if (cacheEntry.State == ServiceState.Created)");
        f.BeginBlock();
        {
            // Already succeeded, ignore subsequent failures (don't overwrite success state)
            f.AppendLine("return;");
        }
        f.EndBlock();
        f.AppendLine("cacheEntry.State = ServiceState.Failed;");
        f.AppendLine();
        f.PrintError(
            "$\"[GodotSharpDI] Host '{providerType}' failed to provide service"
            + "\\n  Reason: Null reference provided for implementation type: {implType.Name}\""
        );
        f.AppendLine();
        f.AppendLine("// Notify waiting waiters: service creation failed, pass null to callback");
        f.AppendLine("if (_waiters.Remove(implType, out var failedWaiters))");
        f.BeginBlock();
        {
            f.AppendLine("foreach (var waiter in failedWaiters)");
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    f.AppendLine("waiter.ResultCallback.Invoke(null);");
                }
                f.CatchBlock("ex");
                {
                    f.PrintError(
                        "$\"[GodotSharpDI] Exception in dependency injection callback (on failure)"
                        + "\\n  Reason: {ex.Message}\""
                    );
                }
                f.EndTryCatch();
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine("return;");
    }

    private static void GenerateProvideSuccess(CodeFormatter f)
    {
        f.AppendLine("// Success scenario");
        f.AppendLine("if (cacheEntry.State == ServiceState.Created)");
        f.BeginBlock();
        {
            f.PrintError(
                "$\"[GodotSharpDI] Duplicate service provision"
                + "\\n  Reason: Service {implType.Name} has already been provided\""
            );
            f.AppendLine("return;");
        }
        f.EndBlock();
        f.AppendLine();
        f.AppendLine("cacheEntry.State = ServiceState.Created;");
        f.AppendLine("cacheEntry.Instance = instance;");
    }

    private static void GenerateProvideWaiterNotification(CodeFormatter f)
    {
        f.AppendLine("if (_waiters.Remove(implType, out var waiters))");
        f.BeginBlock();
        {
            f.AppendLine("foreach (var waiter in waiters)");
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    // instance == null → failure, non-null → successful instance
                    f.AppendLine("waiter.ResultCallback.Invoke(instance);");
                }
                f.CatchBlock("ex");
                {
                    f.PrintError(
                        "$\"[GodotSharpDI] Exception in dependency injection callback"
                        + "\\n  Reason: {ex.Message}\""
                    );
                }
                f.EndTryCatch();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    // =========================================================
    // ResolveDependency<TExposed>(Action<TExposed?> onResult, …)
    // =========================================================

    private static void GenerateResolveDependency(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "Resolve service dependency. TExposed is the exposed interface type, mapped to implementation type via ServiceImplementationMap."
        );
        f.BeginLevel();
        {
            f.AppendLine($"void {GlobalNames.IScope}.ResolveDependency<TExposed>(");
            f.AppendLine($"{GlobalNames.Action}<TExposed?> onResult,");
            f.AppendLine($"{GlobalNames.String} requestorType)");
        }
        f.EndLevel();
        f.AppendTypeConstraints("where TExposed : class");
        f.BeginBlock();
        {
            f.AppendLine("var exposedType = typeof(TExposed);");
            f.AppendLine();

            f.AppendLine($"var currentScopeChain = \"{validatedType.Symbol.Name}\";");
            f.AppendLine(
                "var currentDependencyChain = requestorType + $\" -> {exposedType.Name}\";"
            );
            f.AppendLine();

            // Find implementation type via ServiceImplementationMap
            f.AppendLine(
                "if (!ServiceImplementationMap.TryGetValue(exposedType, out var implType) || "
                    + "!ServiceCache.TryGetValue(implType, out var cacheEntry))"
            );
            f.BeginBlock();
            {
                GenerateServiceNotFoundHandling(f, validatedType);
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("switch (cacheEntry.State)");
            f.BeginBlock();
            {
                GenerateCreatedCase(f, validatedType);
                f.AppendLine();
                GenerateFailedCase(f, validatedType);
                f.AppendLine();
                GenerateNotCreatedCase(f);
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateServiceNotFoundHandling(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendLine("var parent = GetParentScope();", "Forward to parent Scope");
        f.AppendLine("if (parent is not null)");
        f.BeginBlock();
        {
            f.AppendLine("parent.ResolveDependency<TExposed>(onResult, requestorType);");
            f.AppendLine("return;");
        }
        f.EndBlock();
        f.AppendLine();

        f.PrintError(
            "$\"[GodotSharpDI] Cannot find service {exposedType.Name}"
            + "\\n  Reason: No Scope in scene tree contains this service"
            + $"\\n  Scope: {validatedType.Symbol.Name}"
            + "\\n  Requestor: {requestorType}"
            + "\\n  Scope Chain: {currentScopeChain}"
            + "\\n  Dependency Chain: {currentDependencyChain}\""
        );
        f.AppendLine();

        f.BeginTryCatch();
        {
            f.AppendLine("onResult.Invoke(null);");
        }
        f.CatchBlock("ex");
        {
            f.PrintError(
                "$\"[GodotSharpDI] Exception in dependency injection callback"
                + "\\n  Reason: {ex.Message}\""
            );
        }
        f.EndTryCatch();
        f.AppendLine("return;");
    }

    private static void GenerateCreatedCase(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendLine("case ServiceState.Created:");
        f.BeginBlock();
        {
            f.BeginTryCatch();
            {
                f.AppendLine("var __cast = cacheEntry.Instance as TExposed;");
                f.AppendLine("if (__cast is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("onResult.Invoke(__cast);");
                }
                f.EndBlock();
                f.AppendLine("else");
                f.BeginBlock();
                {
                    f.PrintError(
                        "$\"[GodotSharpDI] Type mismatch in dependency injection"
                        + "\\n  Reason: Service implementation type {implType.Name} cannot be cast to {exposedType.Name}"
                        + $"\\n  Scope: {validatedType.Symbol.Name}"
                        + "\\n  Requestor: {requestorType}"
                        + "\\n  Scope Chain: {currentScopeChain}"
                        + "\\n  Dependency Chain: {currentDependencyChain}\""
                    );
                    f.AppendLine("onResult.Invoke(null);");
                }
                f.EndBlock();
            }
            f.CatchBlock("ex");
            {
                f.PrintError(
                    "$\"[GodotSharpDI] Exception in dependency injection callback"
                    + "\\n  Reason: {ex.Message}\""
                );
            }
            f.EndTryCatch();
            f.AppendLine("break;");
        }
        f.EndBlock();
    }

    private static void GenerateFailedCase(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendLine("case ServiceState.Failed:");
        f.BeginBlock();
        {
            f.PrintError(
                "$\"[GodotSharpDI] Previous creation of service {exposedType.Name} failed"
                + "\\n  Reason: The Host reported a null instance"
                + $"\\n  Scope: {validatedType.Symbol.Name}"
                + "\\n  Requestor: {requestorType}"
                + "\\n  Scope Chain: {currentScopeChain}"
                + "\\n  Dependency Chain: {currentDependencyChain}\""
            );
            f.AppendLine();

            f.BeginTryCatch();
            {
                f.AppendLine("onResult.Invoke(null);");
            }
            f.CatchBlock("ex");
            {
                f.PrintError(
                    "$\"[GodotSharpDI] Exception in dependency injection callback"
                    + "\\n  Reason: {ex.Message}\""
                );
            }
            f.EndTryCatch();
            f.AppendLine("break;");
        }
        f.EndBlock();
    }

    private static void GenerateNotCreatedCase(CodeFormatter f)
    {
        f.AppendLine("case ServiceState.NotCreated:");
        f.BeginBlock();
        {
            f.AppendLine("if (!_waiters.TryGetValue(implType, out var waiterList))");
            f.BeginBlock();
            {
                f.AppendLine($"waiterList = new {GlobalNames.List}<DependencyWaitInfo>();");
                f.AppendLine("_waiters[implType] = waiterList;");
            }
            f.EndBlock();
            f.AppendLine();

            f.BeginDebugRegion();
            f.AppendLine("TryTrackAndDetectDeadlock(requestorType, exposedType.Name);");
            f.EndDebugRegion();
            f.AppendLine();

            // ResultCallback: Downcast object? to TExposed?, pass to caller's callback
            f.AppendLine("waiterList.Add(new DependencyWaitInfo(");
            f.BeginLevel();
            {
                f.AppendLine("ResultCallback: obj => onResult.Invoke((TExposed?)obj),");
                f.AppendLine($"RequestTicks: {GlobalNames.DateTime}.Now.Ticks,");
                f.AppendLine("RequestorType: requestorType,");
                f.AppendLine("ScopeChain: currentScopeChain,");
                f.AppendLine("DependencyChain: currentDependencyChain)");
            }
            f.EndLevel();
            f.AppendLine(");");
            f.AppendLine();
            f.AppendLine("break;");
        }
        f.EndBlock();
    }
}
