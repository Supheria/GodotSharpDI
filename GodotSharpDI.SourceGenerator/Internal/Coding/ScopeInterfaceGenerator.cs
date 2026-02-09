using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 接口实现代码生成器
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
        // ProvideService
        f.AppendHiddenMethodCommentAndAttribute("以实现类型（而非暴露类型）提供服务");
        f.AppendLine(
            $"void {GlobalNames.IScope}.ProvideService<T>(T? instance, {GlobalNames.String}? errorMessage)"
        );
        f.AppendTypeConstraints("where T : class");
        f.BeginBlock();
        {
            f.AppendLine("var implType = typeof(T);");
            f.AppendLine();

            f.AppendLine(
                "if (!ServiceCache.TryGetValue(implType, out var cacheEntry))",
                "检查是否是已包含的服务类型"
            );
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试向父 Scope 注册");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.ProvideService(instance, errorMessage);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: \"无法提供服务\",");
                    f.AppendLine(
                        "reason: $\"直到场景树的根节点都没有 Scope 包含服务的实现类型：{implType.Name}\","
                    );
                    f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                    f.AppendLine("requestorType: \"N/A\",");
                    f.AppendLine("scopeChain: \"N/A\",");
                    f.AppendLine("dependencyChain: \"N/A\"");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PushError("sb.ToString()");
                f.AppendLine();

                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("// 检查是失败还是成功场景");
            f.AppendLine("if (instance is null)");
            f.BeginBlock();
            {
                f.AppendLine("// === 失败场景 ===");
                f.AppendLine("// 标记为失败状态");
                f.AppendLine("cacheEntry.State = ServiceState.Failed;");
                f.AppendLine("cacheEntry.FailureReason = errorMessage;");
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
                        f.AppendLine(
                            "cacheEntry.FailureDependencyChains.Add(waiter.DependencyChain);"
                        );
                    }
                    f.EndBlock();
                }
                f.EndBlock();
                f.AppendLine("else");
                f.BeginBlock();
                {
                    f.AppendLine("// 没有等待者（Host主动提供但尚无请求）");
                    f.AppendLine(
                        "cacheEntry.FailureDependencyChains.Add(implType.Name + \" (on provided)\");"
                    );
                }
                f.EndBlock();
                f.AppendLine();
            }
            f.EndBlock();
            f.AppendLine("else");
            f.BeginBlock();
            {
                f.AppendLine("// === 成功场景 ===");
                f.AppendLine("// 已经成功创建过");
                f.AppendLine("if (cacheEntry.State == ServiceState.Created)");
                f.BeginBlock();
                {
                    f.PushError("$\"重复注册类型: {implType.Name}\"");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine("cacheEntry.State = ServiceState.Created;");
                f.AppendLine("cacheEntry.Instance = instance;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("// 通知等待者");
            f.AppendLine("if (_waiters.Remove(implType, out var waiters))");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var waiter in waiters)");
                f.BeginBlock();
                {
                    f.AppendLine("if (instance is null)");
                    f.BeginBlock();
                    {
                        f.AppendLine("// 失败场景");
                        f.AppendLine("var sb = CreateErrorMessageBuilder(");
                        f.BeginLevel();
                        {
                            f.AppendLine("title: \"服务提供失败\",");
                            f.AppendLine("reason: $\"{errorMessage}\",");
                            f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                            f.AppendLine("requestorType: $\"{waiter.RequestorType}\",");
                            f.AppendLine("scopeChain: $\"{waiter.ScopeChain}\",");
                            f.AppendLine("dependencyChain: $\"{waiter.DependencyChain}\"");
                        }
                        f.EndLevel();
                        f.AppendLine(");");
                        f.PushError("sb.ToString()");
                        f.AppendLine();

                        f.AppendLine("// 调用等待者的失败回调");
                        f.BeginTryCatch();
                        {
                            f.AppendLine(
                                "waiter.FailureCallback.Invoke($\"依赖注入请求失败：服务 {implType.Name} 提供失败\");"
                            );
                        }
                        f.CatchBlock("ex");
                        {
                            f.AppendLine("sb = CreateErrorMessageBuilder(");
                            f.BeginLevel();
                            {
                                f.AppendLine("title: \"执行依赖注入失败回调时出现了异常\",");
                                f.AppendLine("reason: $\"{ex.Message}\",");
                                f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                                f.AppendLine("requestorType: $\"{waiter.RequestorType}\",");
                                f.AppendLine("scopeChain: $\"{waiter.ScopeChain}\",");
                                f.AppendLine("dependencyChain: $\"{waiter.DependencyChain}\"");
                            }
                            f.EndLevel();
                            f.AppendLine(");");
                            f.PushError("sb.ToString()");
                        }
                        f.EndTryCatch();
                    }
                    f.EndBlock();
                    f.AppendLine("else");
                    f.BeginBlock();
                    {
                        f.AppendLine("// 成功场景：调用等待者的回调");
                        f.BeginTryCatch();
                        {
                            f.AppendLine("waiter.Callback.Invoke(instance);");
                        }
                        f.CatchBlock("ex");
                        {
                            f.AppendLine("var sb = CreateErrorMessageBuilder(");
                            f.BeginLevel();
                            {
                                f.AppendLine("title: \"执行依赖注入回调时出现了异常\",");
                                f.AppendLine("reason: $\"{ex.Message}\",");
                                f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                                f.AppendLine("requestorType: $\"{waiter.RequestorType}\",");
                                f.AppendLine("scopeChain: $\"{waiter.ScopeChain}\",");
                                f.AppendLine("dependencyChain: $\"{waiter.DependencyChain}\"");
                            }
                            f.EndLevel();
                            f.AppendLine(");");
                            f.PushError("sb.ToString()");
                        }
                        f.EndTryCatch();
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock();
            f.AppendLine();
        }
        f.EndBlock();
    }

    private static void GenerateResolveDependency(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // ResolveDependency
        f.AppendHiddenMethodCommentAndAttribute();
        f.BeginLevel();
        {
            f.AppendLine($"void {GlobalNames.IScope}.ResolveDependency<T>(");
            f.AppendLine($"{GlobalNames.Action}<T> onResolved,");
            f.AppendLine($"{GlobalNames.Action}<{GlobalNames.String}> onFailed,");
            f.AppendLine($"{GlobalNames.String} requestorType, ");
            f.AppendLine($"{GlobalNames.String}? scopeChain, ");
            f.AppendLine($"{GlobalNames.String}? dependencyChain)");
        }
        f.EndLevel();
        f.AppendTypeConstraints("where T : class");
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("// 构建 Scope 传递链");
            f.AppendLine(
                $"var currentScopeChain = scopeChain is null ? \"{validatedType.Symbol.Name}\" : scopeChain + \" -> {validatedType.Symbol.Name}\";"
            );

            f.AppendLine("// 构建依赖链条");
            f.AppendLine(
                "var currentDependencyChain = (dependencyChain ?? requestorType) + $\" -> {type.Name}\";"
            );
            f.AppendLine();

            f.AppendLine(
                "if (!ServiceImplementationMap.TryGetValue(type, out var implType) || !ServiceCache.TryGetValue(implType, out var cacheEntry))",
                "检查是否是已包含的服务类型"
            );
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 解析");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine(
                        "parent.ResolveDependency(onResolved, onFailed, requestorType, currentScopeChain, dependencyChain);"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine("var sb = CreateErrorMessageBuilder(");
                f.BeginLevel();
                {
                    f.AppendLine("title: $\"无法找到服务 {type.Name}\",");
                    f.AppendLine("reason: \"直到场景树的根节点都没有 Scope 包含服务的实现类型\",");
                    f.AppendLine("serviceImplType: \"N/A\",");
                    f.AppendLine("requestorType: requestorType,");
                    f.AppendLine("scopeChain: currentScopeChain,");
                    f.AppendLine("dependencyChain: currentDependencyChain");
                }
                f.EndLevel();
                f.AppendLine(");");
                f.PushError("sb.ToString()");
                f.AppendLine();

                f.AppendLine("// 调用失败回调");
                f.BeginTryCatch();
                {
                    f.AppendLine(
                        "onFailed.Invoke($\"依赖注入请求失败：无法找到服务 {type.Name}\");"
                    );
                }
                f.CatchBlock("ex");
                {
                    f.AppendLine("sb = CreateErrorMessageBuilder(");
                    f.BeginLevel();
                    {
                        f.AppendLine("title: \"执行依赖注入失败回调时出现了异常\",");
                        f.AppendLine("reason: $\"{ex.Message}\",");
                        f.AppendLine("serviceImplType: \"N/A\",");
                        f.AppendLine("requestorType: requestorType,");
                        f.AppendLine("scopeChain: currentScopeChain,");
                        f.AppendLine("dependencyChain: currentDependencyChain");
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                    f.PushError("sb.ToString()");
                }
                f.EndTryCatch();
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("// 状态机处理");
            f.AppendLine("switch (cacheEntry.State)");
            f.BeginBlock();
            {
                // Case: Created
                f.AppendLine("case ServiceState.Created:");
                f.BeginBlock();
                {
                    f.AppendLine("// 直接返回缓存的实例");
                    f.BeginTryCatch();
                    {
                        f.AppendLine("onResolved.Invoke((T)cacheEntry.Instance!);");
                    }
                    f.CatchBlock("ex");
                    {
                        f.AppendLine("var sb = CreateErrorMessageBuilder(");
                        f.BeginLevel();
                        {
                            f.AppendLine("title: \"执行依赖注入回调时出现了异常\",");
                            f.AppendLine("reason: $\"{ex.Message}\",");
                            f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                            f.AppendLine("requestorType: requestorType,");
                            f.AppendLine("scopeChain: currentScopeChain,");
                            f.AppendLine("dependencyChain: currentDependencyChain");
                        }
                        f.EndLevel();
                        f.AppendLine(");");
                        f.PushError("sb.ToString()");
                    }
                    f.EndTryCatch();
                    f.AppendLine("break;");
                }
                f.EndBlock();

                // Case: Failed
                f.AppendLine("case ServiceState.Failed:");
                f.BeginBlock();
                {
                    f.AppendLine("// 报告之前的失败信息");
                    f.AppendLine("var sb = CreateErrorMessageBuilder(");
                    f.BeginLevel();
                    {
                        f.AppendLine("title: $\"先前创建服务 {type.Name} 时失败\",");
                        f.AppendLine("reason: $\"{cacheEntry.FailureReason}\",");
                        f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                        f.AppendLine("requestorType: requestorType,");
                        f.AppendLine("scopeChain: currentScopeChain,");
                        f.AppendLine("dependencyChain: currentDependencyChain");
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                    f.AppendLine("sb.AppendLine(\"  服务创建时已有的依赖链条:\");");
                    f.AppendLine(
                        "for (var i = 0; i < cacheEntry.FailureDependencyChains.Count; i++)"
                    );
                    f.BeginBlock();
                    {
                        f.AppendLine(
                            "sb.AppendLine($\"    [{i + 1}] {cacheEntry.FailureDependencyChains[i]}\");"
                        );
                    }
                    f.EndBlock();
                    f.PushError("sb.ToString()");
                    f.AppendLine();

                    f.AppendLine("// 直接调用失败回调");
                    f.BeginTryCatch();
                    {
                        f.AppendLine(
                            "onFailed.Invoke($\"依赖注入请求失败：先前创建服务 {type.Name} 时失败\");"
                        );
                    }
                    f.CatchBlock("ex");
                    {
                        f.AppendLine("sb = CreateErrorMessageBuilder(");
                        f.BeginLevel();
                        {
                            f.AppendLine("title: \"执行依赖注入失败回调时出现了异常\",");
                            f.AppendLine("reason: $\"{ex.Message}\",");
                            f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                            f.AppendLine("requestorType: requestorType,");
                            f.AppendLine("scopeChain: currentScopeChain,");
                            f.AppendLine("dependencyChain: currentDependencyChain");
                        }
                        f.EndLevel();
                        f.AppendLine(");");
                        f.PushError("sb.ToString()");
                    }
                    f.EndTryCatch();
                    f.AppendLine("break;");
                }
                f.EndBlock();

                // Case: Creating
                f.AppendLine("case ServiceState.Creating:");
                f.BeginBlock();
                {
                    f.BeginDebugRegion();
                    {
                        f.AppendLine("// [DEBUG ONLY] 防御性检查：编译期应该已经捕获所有循环依赖");
                        f.AppendLine("// 如果这里触发，说明编译期分析可能有bug，请报告");
                        f.AppendLine(
                            "if (HasCircularDependency(currentDependencyChain, type.Name))"
                        );
                        f.BeginBlock();
                        {
                            f.AppendLine("var sb = CreateErrorMessageBuilder(");
                            f.BeginLevel();
                            {
                                f.AppendLine(
                                    "title: \"[DEBUG] 运行时检测到循环依赖（编译期应该已阻止）\","
                                );
                                f.AppendLine(
                                    "reason: \"这表明编译期分析可能有问题，请报告此bug\","
                                );
                                f.AppendLine("serviceImplType: $\"{implType.Name}\",");
                                f.AppendLine("requestorType: requestorType,");
                                f.AppendLine("scopeChain: currentScopeChain,");
                                f.AppendLine("dependencyChain: currentDependencyChain");
                            }
                            f.EndLevel();
                            f.AppendLine(");");
                            f.PushError("sb.ToString()");
                            f.AppendLine("break;");
                        }
                        f.EndBlock();
                    }
                    f.EndDebugRegion();
                    f.AppendLine();

                    f.AppendLine("// 服务正在异步创建中，加入等待队列");
                    f.AppendLine("if (!_waiters.TryGetValue(implType, out var waiterList))");
                    f.BeginBlock();
                    {
                        f.AppendLine($"waiterList = new {GlobalNames.List}<DependencyWaitInfo>();");
                        f.AppendLine("_waiters[implType] = waiterList;");
                    }
                    f.EndBlock();
                    f.AppendLine();
                    f.AppendLine("waiterList.Add(new DependencyWaitInfo(");
                    f.BeginLevel();
                    {
                        f.AppendLine("Callback: obj => onResolved.Invoke((T)obj),");
                        f.AppendLine("FailureCallback: onFailed,");
                        f.AppendLine($"RequestTicks: {GlobalNames.DateTime}.Now.Ticks,");
                        f.AppendLine("RequestorType: requestorType,");
                        f.AppendLine("ScopeChain: currentScopeChain,");
                        f.AppendLine("DependencyChain: currentDependencyChain)");
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                    f.AppendLine("break;");
                }
                f.EndBlock();

                // Case: NotCreated
                f.AppendLine("case ServiceState.NotCreated:");
                f.BeginBlock();
                {
                    f.AppendLine("// 服务未准备完成，加入等待队列");
                    f.AppendLine("if (!_waiters.TryGetValue(implType, out var waiterList))");
                    f.BeginBlock();
                    {
                        f.AppendLine($"waiterList = new {GlobalNames.List}<DependencyWaitInfo>();");
                        f.AppendLine("_waiters[implType] = waiterList;");
                    }
                    f.EndBlock();
                    f.AppendLine();

                    f.AppendLine("waiterList.Add(new DependencyWaitInfo(");
                    f.BeginLevel();
                    {
                        f.AppendLine("Callback: obj => onResolved.Invoke((T)obj),");
                        f.AppendLine("FailureCallback: onFailed,");
                        f.AppendLine($"RequestTicks: {GlobalNames.DateTime}.Now.Ticks,");
                        f.AppendLine("RequestorType: requestorType,");
                        f.AppendLine("ScopeChain: currentScopeChain,");
                        f.AppendLine("DependencyChain: currentDependencyChain)");
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                    f.AppendLine();

                    f.AppendLine("// 检查是否有工厂（Scope 创建的单例服务）");
                    f.AppendLine(
                        "if (ServiceFactories.TryGetValue(implType, out var factory) && cacheEntry.State == ServiceState.NotCreated)"
                    );
                    f.BeginBlock();
                    {
                        f.AppendLine("// 按需创建服务");
                        f.AppendLine("cacheEntry.State = ServiceState.Creating;");
                        f.AppendLine();

                        f.AppendLine("// 调用工厂创建服务");
                        f.AppendLine("factory(");
                        f.BeginLevel();
                        {
                            f.AppendLine("this,");
                            f.AppendLine("(instance) =>");
                            f.BeginBlock();
                            {
                                f.AppendLine(
                                    $"if (instance is {GlobalNames.IDisposable} disposable)"
                                );
                                f.BeginBlock();
                                {
                                    f.AppendLine("_disposableSingletons.Add(disposable);");
                                }
                                f.EndBlock();
                            }
                            f.EndBlock(",");
                            f.AppendLine("currentDependencyChain");
                        }
                        f.EndLevel();
                        f.AppendLine(");");
                    }
                    f.EndBlock();
                    f.AppendLine("break;");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成辅助方法（在生成的 Scope 类中）
    /// </summary>
    private static void GenerateHelperMethods(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // CreateErrorMessageBuilder 辅助方法
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
                f.StringBuilderAppendLine("  原因: {reason}");
                f.StringBuilderAppendLine($"  当前 Scope: {validatedType.Symbol.Name}");
                f.StringBuilderAppendLine("  服务的实现类型: {serviceImplType}");
                f.StringBuilderAppendLine("  请求者类型: {requestorType}");
                f.StringBuilderAppendLine("  当前 Scope 传递链: {scopeChain}");
                f.StringBuilderAppendLine("  当前依赖链条: {dependencyChain}");
            }
            f.EndStringBuilderAppend();
            f.AppendLine("return sb;");
        }
        f.EndBlock();

        // HasCircularDependency 辅助方法 (仅 DEBUG 模式)
        f.BeginDebugRegion();
        {
            f.AppendHiddenMethodCommentAndAttribute("检测运行时是否有意外的依赖循环（仅开发模式）");
            f.AppendLine(
                $"private static {GlobalNames.Bool} HasCircularDependency("
                    + $"{GlobalNames.String} dependencyChain, "
                    + $"{GlobalNames.String} newType)"
            );
            f.BeginBlock();
            {
                f.AppendLine("if (string.IsNullOrEmpty(dependencyChain))");
                f.BeginBlock();
                {
                    f.AppendLine("return false;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine(
                    $"var parts = dependencyChain.Split(new[] {{ \" -> \" }}, {GlobalNames.StringSplitOptions}.None);"
                );
                f.AppendLine($"var seen = new {GlobalNames.HashSet}<{GlobalNames.String}>(parts);");
                f.AppendLine("return !seen.Add(newType);");
            }
            f.EndBlock();
        }
        f.EndDebugRegion();
    }
}
