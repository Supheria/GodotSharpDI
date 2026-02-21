using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 接口实现代码生成器
///
/// v1.3.0 重构：移除 ResolutionResult，IScope 直接使用可空类型：
///   ProvideService&lt;TImpl&gt;(TImpl? instance)          — null 表示提供失败
///   ResolveDependency&lt;TExposed&gt;(Action&lt;TExposed?&gt;) — 回调收到 null 表示解析失败
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
            "以实现类型提供服务。instance == null 表示服务创建失败。"
        );
        f.AppendLine($"void {GlobalNames.IScope}.ProvideService<TImpl>(TImpl? instance)");
        f.AppendTypeConstraints("where TImpl : class");
        f.BeginBlock();
        {
            f.AppendLine("var implType = typeof(TImpl);");
            f.AppendLine();

            // 查找 ServiceCache（键是实现类型）
            f.AppendLine("if (!ServiceCache.TryGetValue(implType, out var cacheEntry))");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "向父 Scope 转发");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.ProvideService<TImpl>(instance);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();
                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: \"Cannot provide service\",");
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

            // 处理失败场景（instance == null）
            f.AppendLine("if (instance is null)");
            f.BeginBlock();
            {
                f.AppendLine("// 失败场景：服务创建失败");
                f.AppendLine("if (cacheEntry.State == ServiceState.Created)");
                f.BeginBlock();
                {
                    // 已经成功过了，忽略后续失败（不覆盖成功状态）
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("cacheEntry.State = ServiceState.Failed;");
                f.AppendLine();
                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: \"Service creation failed\",");
                    f.AppendLine("reason: $\"Host provided null for {implType.Name}\",");
                    f.AppendLine("serviceImplType: implType.Name,");
                    f.AppendLine("requestorType: \"N/A\",");
                    f.AppendLine("scopeChain: \"N/A\",");
                    f.AppendLine("dependencyChain: \"N/A\"");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PrintError("sb.ToString()");
            }
            f.EndBlock();
            f.AppendLine("else");
            f.BeginBlock();
            {
                f.AppendLine("// 成功场景");
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

            // 通知所有等待者（键是实现类型）
            f.AppendLine("if (_waiters.Remove(implType, out var waiters))");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var waiter in waiters)");
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        // instance == null → 失败，非 null → 成功的实例
                        f.AppendLine("waiter.ResultCallback.Invoke(instance);");
                    }
                    f.CatchBlock("ex");
                    {
                        f.AppendLine("var sb = CreateErrorMessageBuilder(");
                        f.BeginLevel();
                        {
                            f.AppendLine(
                                "title: \"Exception in dependency injection callback\","
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
        }
        f.EndBlock();
    }

    // =========================================================
    // ResolveDependency<TExposed>(Action<TExposed?> onResult, …)
    // =========================================================

    private static void GenerateResolveDependency(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "解析服务依赖。TExposed 是暴露的接口类型，通过 ServiceImplementationMap 映射到实现类型。"
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

            // 通过 ServiceImplementationMap 查找实现类型
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
        f.AppendLine("var parent = GetParentScope();", "向父 Scope 转发");
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
                f.AppendLine("onResult.Invoke((TExposed)cacheEntry.Instance!);");
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
                f.AppendLine(
                    "title: $\"Previous creation of service {exposedType.Name} failed\","
                );
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

            // ResultCallback: 将 object? 向下转换为 TExposed?，传递给调用者的回调
            f.AppendLine("waiterList.Add(new DependencyWaitInfo(");
            f.BeginLevel();
            {
                f.AppendLine(
                    "ResultCallback: obj => onResult.Invoke((TExposed?)obj),"
                );
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
