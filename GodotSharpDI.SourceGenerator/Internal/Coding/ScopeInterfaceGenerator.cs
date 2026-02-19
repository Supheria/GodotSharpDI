using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 接口实现代码生成器（修复版）
/// 核心修复：ProvideService 明确使用实现类型，与 ServiceCache 和 _waiters 的键类型一致
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

    private static void GenerateProvideService(CodeFormatter f)
    {
        // ProvideService - 使用 ResolutionResult
        f.AppendHiddenMethodCommentAndAttribute(
            "以实现类型提供服务，TImpl 必须是服务的实际实现类型，而非暴露的接口类型"
        );
        f.AppendLine(
            $"void {GlobalNames.IScope}.ProvideService<TImpl>({GlobalNames.ResolutionResult} result)"
        );
        f.AppendTypeConstraints("where TImpl : class");
        f.BeginBlock();
        {
            f.AppendLine("var implType = typeof(TImpl);");
            f.AppendLine();

            // 检查 ServiceCache（键是实现类型）
            f.AppendLine(
                "if (!ServiceCache.TryGetValue(implType, out var cacheEntry))",
                "查找实现类型"
            );
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试向父 Scope 提供");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.ProvideService<TImpl>(result);");
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
                f.AppendLine();

                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("// 处理成功或失败场景");
            f.AppendLine("if (result.IsFailure)");
            f.BeginBlock();
            {
                GenerateFailureScenario(f);
            }
            f.EndBlock();
            f.AppendLine("else");
            f.BeginBlock();
            {
                GenerateSuccessScenario(f);
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("// 通知等待者（键是实现类型）");
            f.AppendLine("if (_waiters.Remove(implType, out var waiters))");
            f.BeginBlock();
            {
                GenerateNotifyWaiters(f);
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateFailureScenario(CodeFormatter f)
    {
        f.AppendLine("// === 失败场景 ===");
        f.AppendLine("cacheEntry.State = ServiceState.Failed;");
        f.AppendLine("cacheEntry.FailureReason = result.ErrorMessage;");
        f.AppendLine();

        f.AppendLine(
            "if (_waiters.TryGetValue(implType, out var waiterList) && waiterList.Count > 0)"
        );
        f.BeginBlock();
        {
            f.AppendLine("// 记录所有已有等待者的依赖链");
            f.AppendLine("foreach (var waiter in waiterList)");
            f.BeginBlock();
            {
                f.AppendLine("cacheEntry.FailureDependencyChains.Add(waiter.DependencyChain);");
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine("else");
        f.BeginBlock();
        {
            f.AppendLine("// 没有等待者（Host主动提供但尚无请求）");
            f.AppendLine(
                "cacheEntry.FailureDependencyChains.Add(implType.Name + \" (provided without request)\");"
            );
        }
        f.EndBlock();
    }

    private static void GenerateSuccessScenario(CodeFormatter f)
    {
        f.AppendLine("// === 成功场景 ===");
        f.AppendLine("if (cacheEntry.State == ServiceState.Created)");
        f.BeginBlock();
        {
            f.AppendLine("var sb = CreateErrorMessageBuilder(");
            f.BeginLevel();
            {
                f.AppendLine("title: \"Duplicate service provision\",");
                f.AppendLine("reason: $\"Service {implType.Name} has already been provided\",");
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
        f.AppendLine("cacheEntry.Instance = result.Instance;");
    }

    private static void GenerateNotifyWaiters(CodeFormatter f)
    {
        f.AppendLine("foreach (var waiter in waiters)");
        f.BeginBlock();
        {
            f.AppendLine("if (result.IsFailure)");
            f.BeginBlock();
            {
                GenerateFailureNotification(f);
            }
            f.EndBlock();
            f.AppendLine("else");
            f.BeginBlock();
            {
                GenerateSuccessNotification(f);
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateFailureNotification(CodeFormatter f)
    {
        f.AppendLine("// 失败通知");
        f.AppendLine("var sb = CreateErrorMessageBuilder(");
        f.BeginLevel();
        {
            f.AppendLine("title: \"Service provision failed\",");
            f.AppendLine("reason: result.ErrorMessage ?? \"unknown reason\",");
            f.AppendLine("serviceImplType: implType.Name,");
            f.AppendLine("requestorType: waiter.RequestorType,");
            f.AppendLine("scopeChain: waiter.ScopeChain,");
            f.AppendLine("dependencyChain: waiter.DependencyChain");
        }
        f.EndLevel();
        f.AppendLine(");");
        f.PrintError("sb.ToString()");
        f.AppendLine();

        f.BeginTryCatch();
        {
            f.AppendLine("waiter.ResultCallback.Invoke(result);");
        }
        f.CatchBlock("ex");
        {
            f.AppendLine("sb = CreateErrorMessageBuilder(");
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

    private static void GenerateSuccessNotification(CodeFormatter f)
    {
        f.AppendLine("// 成功通知");
        f.BeginTryCatch();
        {
            f.AppendLine("waiter.ResultCallback.Invoke(result);");
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

    private static void GenerateResolveDependency(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // ResolveDependency - 使用 ResolutionResult
        f.AppendHiddenMethodCommentAndAttribute(
            "解析服务依赖，TExposed 是暴露的接口类型，会通过 ServiceImplementationMap 转换为实现类型"
        );
        f.BeginLevel();
        {
            f.AppendLine($"void {GlobalNames.IScope}.ResolveDependency<TExposed>(");
            f.AppendLine($"{GlobalNames.Action}<{GlobalNames.ResolutionResult}> onResult,");
            f.AppendLine($"{GlobalNames.String} requestorType)");
        }
        f.EndLevel();
        f.AppendTypeConstraints("where TExposed : class");
        f.BeginBlock();
        {
            f.AppendLine("var exposedType = typeof(TExposed);", "TExposed 是暴露类型");
            f.AppendLine();

            f.AppendLine("// 构建 Scope 传递链");
            f.AppendLine($"var currentScopeChain = \"{validatedType.Symbol.Name}\";");

            f.AppendLine("// 构建依赖链条");
            f.AppendLine(
                "var currentDependencyChain = requestorType + $\" -> {exposedType.Name}\";"
            );
            f.AppendLine();

            f.AppendLine("// 通过 ServiceImplementationMap 将暴露类型转换为实现类型");
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

            f.AppendLine("// 状态机处理");
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

    private static void GenerateServiceNotFoundHandling(
        CodeFormatter f,
        ValidatedTypeInfo validatedType
    )
    {
        f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 解析");
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

        f.AppendLine("// 调用结果回调");
        f.BeginTryCatch();
        {
            f.AppendLine($"var failureResult = {GlobalNames.ResolutionResult}.Failure(");
            f.AppendLine("    $\"依赖注入请求失败：Cannot find service {exposedType.Name}\");");
            f.AppendLine("onResult.Invoke(failureResult);");
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
            f.AppendLine("// 直接返回缓存的实例");
            f.BeginTryCatch();
            {
                f.AppendLine(
                    $"var successResult = {GlobalNames.ResolutionResult}.Success((TExposed)cacheEntry.Instance!);"
                );
                f.AppendLine("onResult.Invoke(successResult);");
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
            f.AppendLine("// 报告之前的失败信息");
            f.AppendLine("var sb = CreateErrorMessageBuilder(");
            f.BeginLevel();
            {
                f.AppendLine("title: $\"Previous creation of service {exposedType.Name} failed\",");
                f.AppendLine("reason: cacheEntry.FailureReason ?? \"unknown reason\",");
                f.AppendLine("serviceImplType: implType.Name,");
                f.AppendLine("requestorType: requestorType,");
                f.AppendLine("scopeChain: currentScopeChain,");
                f.AppendLine("dependencyChain: currentDependencyChain");
            }
            f.EndLevel();
            f.AppendLine(");");
            f.AppendLine("sb.AppendLine(\"  Prior dependency chains when service was created:\");");
            f.AppendLine("for (var i = 0; i < cacheEntry.FailureDependencyChains.Count; i++)");
            f.BeginBlock();
            {
                f.AppendLine(
                    "sb.AppendLine($\"    [{i + 1}] {cacheEntry.FailureDependencyChains[i]}\");"
                );
            }
            f.EndBlock();
            f.PrintError("sb.ToString()");
            f.AppendLine();

            f.AppendLine("// 调用结果回调");
            f.BeginTryCatch();
            {
                f.AppendLine($"var failureResult = {GlobalNames.ResolutionResult}.Failure(");
                f.AppendLine("    $\"依赖注入请求失败：Previous creation of service {exposedType.Name} failed\");");
                f.AppendLine("onResult.Invoke(failureResult);");
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
            f.AppendLine("// Service not yet ready, add to wait queue (key is impl type)");
            f.AppendLine("if (!_waiters.TryGetValue(implType, out var waiterList))");
            f.BeginBlock();
            {
                f.AppendLine($"waiterList = new {GlobalNames.List}<DependencyWaitInfo>();");
                f.AppendLine("_waiters[implType] = waiterList;");
            }
            f.EndBlock();
            f.AppendLine();

            // P1-runtime: 追踪等待关系并执行 DFS 死锁检测
            f.BeginDebugRegion();
            f.AppendLine("TryTrackAndDetectDeadlock(requestorType, exposedType.Name);");
            f.EndDebugRegion();
            f.AppendLine();

            f.AppendLine("waiterList.Add(new DependencyWaitInfo(");
            f.BeginLevel();
            {
                f.AppendLine(
                    $"ResultCallback: obj => onResult.Invoke(({GlobalNames.ResolutionResult})obj),"
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
                f.StringBuilderAppendLine($"  当前 Scope: {validatedType.Symbol.Name}");
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
