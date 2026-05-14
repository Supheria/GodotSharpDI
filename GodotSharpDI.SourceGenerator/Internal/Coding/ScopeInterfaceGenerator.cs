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
            Generate(f, node);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.IScope.g.cs", f.ToString());
    }

    private static void Generate(CodeFormatter f, ScopeNode node)
    {
        GenerateHelperMethods(f, node.ValidatedTypeInfo);
        f.AppendLine();

        GenerateProvideService(f);
        f.AppendLine();

        GenerateResolveDependency(f, node.ValidatedTypeInfo);
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
                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: $\"Host '{providerType}' cannot provide service\",");
                    f.AppendLine(
                        "reason: $\"No Scope in scene tree contains implementation type: {implType.Name}\","
                    );
                    f.AppendLine("serviceImplType: implType.Name,");
                    f.AppendLine("requestorType: \"N/A\",");
                    f.AppendLine("scopeChain: \"N/A\",");
                    f.AppendLine("dependencyChain: \"N/A\"");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PrintError("sb.ToString()");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // Handle failure scenario (instance == null)
            f.AppendLine("if (instance is null)");
            f.BeginBlock();
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
                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: $\"Host '{providerType}' failed to provide service\",");
                    f.AppendLine(
                        "reason: $\"Null reference provided for implementation type: {implType.Name}\","
                    );
                    f.AppendLine("serviceImplType: implType.Name,");
                    f.AppendLine("requestorType: \"N/A\",");
                    f.AppendLine("scopeChain: \"N/A\",");
                    f.AppendLine("dependencyChain: \"N/A\"");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PrintError("sb.ToString()");
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
                            f.AppendLine("sb = CreateErrorMessageBuilder(");
                            f.BeginLevel();
                            {
                                f.AppendLine(
                                    "title: \"Exception in dependency injection callback (on failure)\","
                                );
                                f.AppendLine("reason: ex.Message,");
                                f.AppendLine("serviceImplType: implType.Name,");
                                f.AppendLine("requestorType: waiter.RequestorType,");
                                f.AppendLine("scopeChain: waiter.ScopeChain,");
                                f.AppendLine("dependencyChain: waiter.DependencyChain");
                            }
                            f.EndLevel();
                            f.AppendLine(");");
                            f.PrintError("sb.ToString()");
                        }
                        f.EndTryCatch();
                    }
                    f.EndBlock();
                }
                f.EndBlock();
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine("else");
            f.BeginBlock();
            {
                f.AppendLine("// Success scenario");
                f.AppendLine("if (cacheEntry.State == ServiceState.Created)");
                f.BeginBlock();
                {
                    f.AppendLine("var sb = CreateErrorMessageBuilder(");
                    f.BeginLevel();
                    {
                        f.AppendLine("title: \"Duplicate service provision\",");
                        f.AppendLine(
                            "reason: $\"Service {implType.Name} has already been provided\","
                        );
                        f.AppendLine("serviceImplType: implType.Name,");
                        f.AppendLine("requestorType: \"N/A\",");
                        f.AppendLine("scopeChain: \"N/A\",");
                        f.AppendLine("dependencyChain: \"N/A\"");
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                    f.PrintError("sb.ToString()");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();
                f.AppendLine("cacheEntry.State = ServiceState.Created;");
                f.AppendLine("cacheEntry.Instance = instance;");
            }
            f.EndBlock();
            f.AppendLine();

            // Notify all waiters (key is implementation type)
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
                        f.AppendLine("var sb = CreateErrorMessageBuilder(");
                        f.BeginLevel();
                        {
                            f.AppendLine("title: \"Exception in dependency injection callback\",");
                            f.AppendLine("reason: ex.Message,");
                            f.AppendLine("serviceImplType: implType.Name,");
                            f.AppendLine("requestorType: waiter.RequestorType,");
                            f.AppendLine("scopeChain: waiter.ScopeChain,");
                            f.AppendLine("dependencyChain: waiter.DependencyChain");
                        }
                        f.EndLevel();
                        f.AppendLine(");");
                        f.PrintError("sb.ToString()");
                    }
                    f.EndTryCatch();
                }
                f.EndBlock();
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
                GenerateServiceNotFoundHandling(f);
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("switch (cacheEntry.State)");
            f.BeginBlock();
            {
                GenerateCreatedCase(f);
                f.AppendLine();
                GenerateFailedCase(f);
                f.AppendLine();
                GenerateNotCreatedCase(f);
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateServiceNotFoundHandling(CodeFormatter f)
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

        f.AppendLine("var sb = CreateErrorMessageBuilder(");
        f.BeginLevel();
        {
            f.AppendLine("title: $\"Cannot find service {exposedType.Name}\",");
            f.AppendLine("reason: \"No Scope in scene tree contains this service\",");
            f.AppendLine("serviceImplType: \"N/A\",");
            f.AppendLine("requestorType: requestorType,");
            f.AppendLine("scopeChain: currentScopeChain,");
            f.AppendLine("dependencyChain: currentDependencyChain");
        }
        f.EndLevel();
        f.AppendLine(");");
        f.PrintError("sb.ToString()");
        f.AppendLine();

        f.BeginTryCatch();
        {
            f.AppendLine("onResult.Invoke(null);");
        }
        f.CatchBlock("ex");
        {
            f.AppendLine("sb = CreateErrorMessageBuilder(");
            f.BeginLevel();
            {
                f.AppendLine("title: \"Exception in dependency injection callback\",");
                f.AppendLine("reason: ex.Message,");
                f.AppendLine("serviceImplType: \"N/A\",");
                f.AppendLine("requestorType: requestorType,");
                f.AppendLine("scopeChain: currentScopeChain,");
                f.AppendLine("dependencyChain: currentDependencyChain");
            }
            f.EndLevel();
            f.AppendLine(");");
            f.PrintError("sb.ToString()");
        }
        f.EndTryCatch();
        f.AppendLine("return;");
    }

    private static void GenerateCreatedCase(CodeFormatter f)
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
                    f.AppendLine("var __sb = CreateErrorMessageBuilder(");
                    f.BeginLevel();
                    {
                        f.AppendLine("title: $\"Type mismatch in dependency injection\",");
                        f.AppendLine(
                            "reason: $\"Service implementation type {implType.Name} cannot be cast to {exposedType.Name}\","
                        );
                        f.AppendLine("serviceImplType: implType.Name,");
                        f.AppendLine("requestorType: requestorType,");
                        f.AppendLine("scopeChain: currentScopeChain,");
                        f.AppendLine("dependencyChain: currentDependencyChain");
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                    f.PrintError("__sb.ToString()");
                    f.AppendLine("onResult.Invoke(null);");
                }
                f.EndBlock();
            }
            f.CatchBlock("ex");
            {
                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: \"Exception in dependency injection callback\",");
                    f.AppendLine("reason: ex.Message,");
                    f.AppendLine("serviceImplType: implType.Name,");
                    f.AppendLine("requestorType: requestorType,");
                    f.AppendLine("scopeChain: currentScopeChain,");
                    f.AppendLine("dependencyChain: currentDependencyChain");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PrintError("sb.ToString()");
            }
            f.EndTryCatch();
            f.AppendLine("break;");
        }
        f.EndBlock();
    }

    private static void GenerateFailedCase(CodeFormatter f)
    {
        f.AppendLine("case ServiceState.Failed:");
        f.BeginBlock();
        {
            f.AppendLine("var sb = CreateErrorMessageBuilder(");
            f.BeginLevel();
            {
                f.AppendLine("title: $\"Previous creation of service {exposedType.Name} failed\",");
                f.AppendLine("reason: \"The Host reported a null instance\",");
                f.AppendLine("serviceImplType: implType.Name,");
                f.AppendLine("requestorType: requestorType,");
                f.AppendLine("scopeChain: currentScopeChain,");
                f.AppendLine("dependencyChain: currentDependencyChain");
            }
            f.EndLevel();
            f.AppendLine(");");
            f.PrintError("sb.ToString()");
            f.AppendLine();

            f.BeginTryCatch();
            {
                f.AppendLine("onResult.Invoke(null);");
            }
            f.CatchBlock("ex");
            {
                f.AppendLine("sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: \"Exception in dependency injection callback\",");
                    f.AppendLine("reason: ex.Message,");
                    f.AppendLine("serviceImplType: implType.Name,");
                    f.AppendLine("requestorType: requestorType,");
                    f.AppendLine("scopeChain: currentScopeChain,");
                    f.AppendLine("dependencyChain: currentDependencyChain");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PrintError("sb.ToString()");
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

    // =========================================================
    // Helper
    // =========================================================

    private static void GenerateHelperMethods(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"private static {GlobalNames.StringBuilder} CreateErrorMessageBuilder("
                + $"{GlobalNames.String} title, "
                + $"{GlobalNames.String} reason, "
                + $"{GlobalNames.String} serviceImplType, "
                + $"{GlobalNames.String} requestorType, "
                + $"{GlobalNames.String} scopeChain, "
                + $"{GlobalNames.String} dependencyChain)"
        );
        f.BeginBlock();
        {
            f.BeginStringBuilderAppend("sb", true);
            {
                f.StringBuilderAppendLine("[GodotSharpDI] {title}");
                f.StringBuilderAppendLine("  Reason: {reason}");
                f.StringBuilderAppendLine($"  Scope: {validatedType.Symbol.Name}");
                f.StringBuilderAppendLine("  Impl Type: {serviceImplType}");
                f.StringBuilderAppendLine("  Requestor: {requestorType}");
                f.StringBuilderAppendLine("  Scope Chain: {scopeChain}");
                f.StringBuilderAppendLine("  Dependency Chain: {dependencyChain}");
            }
            f.EndStringBuilderAppend();
            f.AppendLine("return sb;");
        }
        f.EndBlock();
    }
}
