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
        GenerateCreateErrorMessageBuilder(f, node.ValidatedTypeInfo);
        f.AppendLine();

        GenerateProvideService(f);
        f.AppendLine();

        GenerateResolveDependency(f, node.ValidatedTypeInfo);
    }

    private static void GenerateCreateErrorMessageBuilder(
        CodeFormatter f,
        ValidatedTypeInfo validatedType
    )
    {
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

                f.AppendLine(
                    "var sb = CreateErrorMessageBuilder($\"无法提供服务\", $\"直到场景树的根节点都没有 Scope 包含服务的实现类型：{implType.Name}\", $\"{implType.Name}\", \"none\", \"none\", \"none\");"
                );
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
                        f.AppendLine("// 失败场景：通知等待者服务提供失败");
                        f.AppendLine(
                            "var sb = CreateErrorMessageBuilder(\"服务提供失败\", $\"{errorMessage}\", $\"{implType.Name}\", $\"{waiter.RequestorType}\", $\"{waiter.ScopeChain}\", $\"{waiter.DependencyChain}\");"
                        );
                        f.PushError("sb.ToString()");
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
                            f.AppendLine(
                                "var sb = CreateErrorMessageBuilder(\"执行依赖注入回调时出现了异常\", $\"{ex.Message}\", $\"{implType.Name}\", $\"{waiter.RequestorType}\", $\"{waiter.ScopeChain}\", $\"{waiter.DependencyChain}\");"
                            );
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
        f.AppendLine(
            $"void {GlobalNames.IScope}.ResolveDependency<T>("
                + $"{GlobalNames.Action}<T> onResolved, "
                + $"{GlobalNames.String} requestorType, "
                + $"{GlobalNames.String}? scopeChain, "
                + $"{GlobalNames.String}? dependencyChain)"
        );
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
                "if (!ServiceImplementationMap.TryGetValue(type, out var implType) || !ServiceCache.TryGetValue(implType, out var cacheEntry) )",
                "检查是否是已包含的服务类型"
            );
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 解析");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine(
                        "parent.ResolveDependency(onResolved, requestorType, currentScopeChain, dependencyChain);"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine(
                    "var sb = CreateErrorMessageBuilder($\"无法找到服务 {type.Name}\", $\"直到场景树的根节点都没有 Scope 包含服务的实现类型\", \"unknown\", \"none\", \"none\", \"none\");"
                );
                f.PushError("sb.ToString()");
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
                        f.AppendLine(
                            "var sb = CreateErrorMessageBuilder(\"执行依赖注入回调时出现了异常\", $\"{ex.Message}\", $\"{implType.Name}\", $\"{requestorType}\", $\"{currentScopeChain}\", $\"{currentDependencyChain}\");"
                        );
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
                    f.AppendLine(
                        "var sb = CreateErrorMessageBuilder($\"先前创建服务 {type.Name} 时失败\", $\"{cacheEntry.FailureReason}\", $\"{implType.Name}\", $\"{requestorType}\", $\"{currentScopeChain}\", $\"{currentDependencyChain}\");"
                    );
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
                    f.AppendLine("break;");
                }
                f.EndBlock();

                // Case: Creating
                f.AppendLine("case ServiceState.Creating:");
                f.BeginBlock();
                {
                    f.AppendLine("// 服务正在创建中");
                    f.AppendLine("// 检查是否是真正的循环依赖（依赖链中包含当前类型超过一次）");
                    f.AppendLine("// 为避免误判，将链条两端补齐分隔符，并检查当前类型是否出现多次");
                    f.AppendLine("var chain = \" -> \" + currentDependencyChain + \" -> \";");
                    f.AppendLine("var marker = \" -> \" + type.Name + \" -> \";");
                    f.AppendLine(
                        "var firstIndex = chain.IndexOf(marker, global::System.StringComparison.Ordinal);"
                    );
                    f.AppendLine(
                        "var lastIndex = chain.LastIndexOf(marker, global::System.StringComparison.Ordinal);"
                    );
                    f.AppendLine("var isCircular = firstIndex != lastIndex;");
                    f.AppendLine();
                    f.AppendLine("if (isCircular)");
                    f.BeginBlock();
                    {
                        f.AppendLine("// 真正的循环依赖");
                        f.BeginStringBuilderAppend("errorMessage", true);
                        {
                            f.StringBuilderAppendLine("[GodotSharpDI] 检测到运行时循环依赖");
                            f.StringBuilderAppendLine("  依赖链条: {currentDependencyChain}");
                        }
                        f.EndStringBuilderAppend();
                        f.AppendLine();
                        f.PushError("errorMessage.ToString()");
                    }
                    f.EndBlock();
                    f.AppendLine("else");
                    f.BeginBlock();
                    {
                        f.AppendLine("// 不是循环依赖，服务正在异步创建中，加入等待队列");
                        f.AppendLine("if (!_waiters.TryGetValue(implType, out var waiterList))");
                        f.BeginBlock();
                        {
                            f.AppendLine(
                                $"waiterList = new {GlobalNames.List}<DependencyWaitInfo>();"
                            );
                            f.AppendLine("_waiters[implType] = waiterList;");
                        }
                        f.EndBlock();
                        f.AppendLine();
                        f.AppendLine("waiterList.Add(new DependencyWaitInfo");
                        f.BeginBlock();
                        {
                            f.AppendLine("Callback = obj => onResolved.Invoke((T)obj),");
                            f.AppendLine($"RequestTicks = {GlobalNames.DateTime}.Now.Ticks,");
                            f.AppendLine("RequestorType = requestorType,");
                            f.AppendLine("ScopeChain = currentScopeChain,");
                            f.AppendLine("DependencyChain = currentDependencyChain,");
                        }
                        f.EndBlock(");");
                    }
                    f.EndBlock();
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

                    f.AppendLine("waiterList.Add(new DependencyWaitInfo");
                    f.BeginBlock();
                    {
                        f.AppendLine("Callback = obj => onResolved.Invoke((T)obj),");
                        f.AppendLine($"RequestTicks = {GlobalNames.DateTime}.Now.Ticks,");
                        f.AppendLine("RequestorType = requestorType,");
                        f.AppendLine("ScopeChain = currentScopeChain,");
                        f.AppendLine("DependencyChain = currentDependencyChain,");
                    }
                    f.EndBlock(");");

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
            f.AppendLine();
        }
        f.EndBlock();
    }
}
