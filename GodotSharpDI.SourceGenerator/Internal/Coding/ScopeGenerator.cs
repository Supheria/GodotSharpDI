using System.Collections.Generic;
using System.Text;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 代码生成器
/// </summary>
internal static class ScopeGenerator
{
    public static void Generate(SourceProductionContext context, ScopeNode node, DiGraph graph)
    {
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        ScopeInterfaceGenerator.GenerateInterface(context, node);

        // 生成 Scope 特定代码
        GenerateScopeSpecific(context, node, graph);
    }

    public static void GenerateScopeSpecific(
        SourceProductionContext context,
        ScopeNode node,
        DiGraph graph
    )
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
        {
            GenerateDataModels(f);
            f.AppendLine();

            GenerateDelegates(f);
            f.AppendLine();

            GenerateStaticCollections(f);
            f.AppendLine();

            GenerateInstanceFields(f);
            f.AppendLine();

            GenerateStaticMethods(f, node, graph);
            f.AppendLine();

            GenerateDisposeScopeSingletons(f, node.ValidatedTypeInfo);
            f.AppendLine();

            GenerateDependencyMonitoringMethods(f, node.ValidatedTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Scope.g.cs", f.ToString());
    }

    private static void GenerateDataModels(CodeFormatter f)
    {
        // ServiceState 枚举
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine("private enum ServiceState");
        f.BeginBlock();
        {
            f.AppendLine("NotCreated,  // 未创建");
            f.AppendLine("Creating,    // 创建中");
            f.AppendLine("Created,     // 已创建");
            f.AppendLine("Failed       // 创建失败");
        }
        f.EndBlock();
        f.AppendLine();

        // ServiceCacheEntry 类
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine("private sealed class ServiceCacheEntry");
        f.BeginBlock();
        {
            f.AppendLine("public ServiceState State = ServiceState.NotCreated;");
            f.AppendLine($"public {GlobalNames.Object}? Instance = null;");
            f.AppendLine($"public {GlobalNames.String}? FailureReason = null;");
            f.AppendLine(
                $"public {GlobalNames.List}<{GlobalNames.String}> FailureDependencyChains = new();"
            );
        }
        f.EndBlock();
        f.AppendLine();

        // DependencyWaitInfo 记录
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine("private sealed record DependencyWaitInfo(");
        f.BeginLevel();
        {
            f.AppendLine($"{GlobalNames.Action}<{GlobalNames.Object}> Callback,");
            f.AppendLine($"{GlobalNames.Action}<{GlobalNames.String}> FailureCallback,");
            f.AppendLine($"{GlobalNames.Long} RequestTicks,");
            f.AppendLine($"{GlobalNames.String} RequestorType,");
            f.AppendLine($"{GlobalNames.String} ScopeChain,");
            f.AppendLine($"{GlobalNames.String} DependencyChain");
        }
        f.EndLevel();
        f.AppendLine(");");
    }

    private static void GenerateDelegates(CodeFormatter f)
    {
        // ServiceFactory 委托
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private delegate void ServiceFactory({GlobalNames.IScope} scope, "
                + $"{GlobalNames.Action}<{GlobalNames.Object}> onCreated, "
                + $"{GlobalNames.String}? dependencyChain);"
        );
    }

    private static void GenerateStaticCollections(CodeFormatter f)
    {
        // ServiceImplementationMap
        f.AppendHiddenMemberCommentAndAttribute("服务类型映射表：暴露类型 -> 实现类型");
        f.AppendLine(
            $"private static readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Type}> ServiceImplementationMap = CreateServiceImplementationMap();"
        );
        f.AppendLine();

        // ServiceFactories
        f.AppendHiddenMemberCommentAndAttribute("单例服务创建工厂集合（使用实现类型作为键值）");
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, ServiceFactory> ServiceFactories = CreateServiceFactories();"
        );
    }

    private static void GenerateInstanceFields(CodeFormatter f)
    {
        // ServiceCache
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, ServiceCacheEntry> ServiceCache = CreateServiceCache();"
        );
        f.AppendLine();

        // _waiters
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.List}<DependencyWaitInfo>> _waiters = new();"
        );
        f.AppendLine();

        // _disposableSingletons
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.IDisposable}> _disposableSingletons = new();"
        );

        // _dependencyCheckTimer
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.GodotTimer}? _dependencyCheckTimer;");
    }

    private static void GenerateStaticMethods(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        var serviceImplementationMap = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(
            SymbolEqualityComparer.Default
        );
        var implTypes = new List<INamedTypeSymbol>();

        // 从 Services 的服务中收集
        foreach (var serviceType in node.InstantiateServices)
        {
            if (graph.ServiceNodeMap.TryGetValue(serviceType, out var serviceNode))
            {
                implTypes.Add(serviceType);

                // 添加所有暴露类型到实现类型的映射
                foreach (var exposedType in serviceNode.ProvidedServices)
                {
                    serviceImplementationMap.Add(exposedType, serviceType);
                }
            }
        }

        // 从 Hosts 的 Host 中收集
        foreach (var hostType in node.ExpectHosts)
        {
            if (
                graph.HostNodeMap.TryGetValue(hostType, out var hostNode)
                || graph.HostAndUserNodeMap.TryGetValue(hostType, out hostNode)
            )
            {
                implTypes.Add(hostType);

                // 添加 Host 或 HostAndUser 提供的所有服务类型
                foreach (var exposedType in hostNode.ProvidedServices)
                {
                    serviceImplementationMap.Add(exposedType, hostType);
                }
            }
        }

        // CreateServiceCache
        f.AppendHiddenMethodCommentAndAttribute("初始化所有服务缓存（以实现类型作为键值）");
        f.AppendLine(
            $"private static {GlobalNames.Dictionary}<{GlobalNames.Type}, ServiceCacheEntry> CreateServiceCache()"
        );
        f.BeginBlock();
        {
            f.AppendLine(
                $"var serviceImplementationMap = new {GlobalNames.Dictionary}<{GlobalNames.Type}, ServiceCacheEntry>();"
            );
            f.AppendLine();

            foreach (var type in implTypes)
            {
                f.AppendLine(
                    $"serviceImplementationMap[typeof({type.ToFullyQualifiedName()})] = new();"
                );
            }
            f.AppendLine();

            f.AppendLine("return serviceImplementationMap;");
        }
        f.EndBlock();

        // CreateServiceImplementationMap
        f.AppendHiddenMethodCommentAndAttribute("添加已包含的所有暴露类型所对应的实现类型");
        f.AppendLine(
            $"private static {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Type}> CreateServiceImplementationMap()"
        );
        f.BeginBlock();
        {
            f.AppendLine(
                $"var serviceImplementationMap = new {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Type}>();"
            );
            f.AppendLine();

            foreach (var kvp in serviceImplementationMap)
            {
                f.AppendLine(
                    $"serviceImplementationMap[typeof({kvp.Key.ToFullyQualifiedName()})] = typeof({kvp.Value.ToFullyQualifiedName()});"
                );
            }
            f.AppendLine();

            f.AppendLine("return serviceImplementationMap;");
        }
        f.EndBlock();

        // CreateServiceFactories
        f.AppendHiddenMethodCommentAndAttribute("注册所有 Scope 约束的服务工厂（按需创建）");
        f.AppendLine(
            $"private static {GlobalNames.Dictionary}<{GlobalNames.Type}, ServiceFactory> CreateServiceFactories()"
        );
        f.BeginBlock();
        {
            f.AppendLine(
                $"var serviceFactories = new {GlobalNames.Dictionary}<{GlobalNames.Type}, ServiceFactory>();"
            );
            f.AppendLine();

            foreach (var serviceType in node.InstantiateServices)
            {
                if (!graph.ServiceNodeMap.ContainsKey(serviceType))
                {
                    continue;
                }

                // 通过实现类型注册工厂
                var implType = serviceType.ToFullyQualifiedName();
                f.AppendLine($"serviceFactories[typeof({implType})] = {implType}.CreateService;");
            }
            f.AppendLine();

            f.AppendLine("return serviceFactories;");
        }
        f.EndBlock();
    }

    private static void GenerateDisposeScopeSingletons(
        CodeFormatter f,
        ValidatedTypeInfo validatedType
    )
    {
        // DisposeScopeSingletons
        f.AppendHiddenMethodCommentAndAttribute("释放所有 Scope 约束的单例服务实例");
        f.AppendLine("private void DisposeScopeSingletons()");
        f.BeginBlock();
        {
            f.AppendLine("foreach (var disposable in _disposableSingletons)");
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    f.AppendLine("disposable.Dispose();");
                }
                f.CatchBlock("ex");
                {
                    f.BeginStringBuilderAppend("errorMsg", true);
                    {
                        f.StringBuilderAppendLine(
                            $"[{ShortNames.GodotSharpDI}] 单例服务释放资源失败"
                        );
                        f.StringBuilderAppendLine($"  当前 Scope: {validatedType.Symbol.Name}");
                        f.StringBuilderAppendLine("  服务类型: {disposable.GetType().Name}");
                        f.StringBuilderAppendLine("  异常: {ex.Message}");
                    }
                    f.EndStringBuilderAppend();
                    f.AppendLine();

                    f.PushError("errorMsg.ToString()");
                }
                f.EndTryCatch();
            }
            f.EndBlock();

            f.AppendLine("_disposableSingletons.Clear();");
            f.AppendLine("ServiceCache.Clear();");
        }
        f.EndBlock();
    }

    private static void GenerateDependencyMonitoringMethods(
        CodeFormatter f,
        ValidatedTypeInfo validatedType
    )
    {
        // StartDependencyMonitoring
        f.AppendHiddenMethodCommentAndAttribute("启动依赖监控（仅在开发模式）");
        f.AppendLine("private void StartDependencyMonitoring()");
        f.BeginBlock();
        {
            f.BeginDebugRegion();
            {
                f.AppendLine("if (_dependencyCheckTimer != null) return;");
                f.AppendLine();
                f.AppendLine("_dependencyCheckTimer = new Godot.Timer();");
                f.AppendLine("_dependencyCheckTimer.WaitTime = 5.0;");
                f.AppendLine("_dependencyCheckTimer.Timeout += CheckPendingDependencies;");
                f.AppendLine("AddChild(_dependencyCheckTimer);");
                f.AppendLine("_dependencyCheckTimer.Start();");
            }
            f.EndDebugRegion();
        }
        f.EndBlock();
        f.AppendLine();

        // StopDependencyMonitoring
        f.AppendHiddenMethodCommentAndAttribute("停止依赖监控（仅在开发模式）");
        f.AppendLine("private void StopDependencyMonitoring()");
        f.BeginBlock();
        {
            f.BeginDebugRegion();
            {
                f.AppendLine("if (_dependencyCheckTimer != null)");
                f.BeginBlock();
                {
                    f.AppendLine("_dependencyCheckTimer.Stop();");
                    f.AppendLine("_dependencyCheckTimer.QueueFree();");
                    f.AppendLine("_dependencyCheckTimer = null;");
                }
                f.EndBlock();
            }
            f.EndDebugRegion();
        }
        f.EndBlock();
        f.AppendLine();

        // CheckPendingDependencies
        f.AppendHiddenMethodCommentAndAttribute("检查待处理的依赖（仅在开发模式定期调用）");
        f.AppendLine("private void CheckPendingDependencies()");
        f.BeginBlock();
        {
            f.BeginDebugRegion();
            {
                f.AppendLine("if (_waiters.Count == 0) return;");
                f.AppendLine();
                f.AppendLine($"var now = {GlobalNames.DateTime}.Now.Ticks;");
                f.AppendLine($"var timeout = {GlobalNames.TimeSpan}.FromSeconds(10).Ticks;");
                f.AppendLine();
                f.AppendLine("foreach (var kvp in _waiters)");
                f.BeginBlock();
                {
                    f.AppendLine("var type = kvp.Key;");
                    f.AppendLine("var waiters = kvp.Value;");
                    f.AppendLine();
                    f.AppendLine("foreach (var waiter in waiters)");
                    f.BeginBlock();
                    {
                        f.AppendLine("var elapsed = now - waiter.RequestTicks;");
                        f.AppendLine("if (elapsed > timeout)");
                        f.BeginBlock();
                        {
                            f.AppendLine(
                                $"var elapsedSeconds = {GlobalNames.TimeSpan}.FromTicks(elapsed).TotalSeconds;"
                            );
                            f.BeginStringBuilderAppend("message", true);
                            {
                                f.StringBuilderAppendLine("[GodotSharpDI] 依赖注入超时");
                                f.StringBuilderAppendLine(
                                    $"  当前 Scope: {validatedType.Symbol.Name}"
                                );
                                f.StringBuilderAppendLine("  服务类型: {type.Name}");
                                f.StringBuilderAppendLine("  请求者类型: {waiter.RequestorType}");
                                f.StringBuilderAppendLine("  等待时间: {elapsedSeconds:F1}秒)");
                                f.StringBuilderAppendLine("  Scope 传递链: {waiter.ScopeChain}");
                                f.StringBuilderAppendLine("  依赖链条: {waiter.DependencyChain}");
                            }
                            f.EndStringBuilderAppend();
                            f.AppendLine();

                            f.PushWarning("message");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndDebugRegion();
        }
        f.EndBlock();
        f.AppendLine();

        // ReportUnresolvedDependencies
        f.AppendHiddenMethodCommentAndAttribute("报告所有未解决的依赖（仅在开发模式）");
        f.AppendLine("public void ReportUnresolvedDependencies()");
        f.BeginBlock();
        {
            f.AppendLine("if (_waiters.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.BeginStringBuilderAppend("message", true);
            {
                f.StringBuilderAppendLine(
                    $"[GodotSharpDI] {validatedType.Symbol.Name} 存在未解决的依赖"
                );
            }
            f.EndStringBuilderAppend();
            f.AppendLine();

            f.AppendLine("foreach (var kvp in _waiters)");
            f.BeginBlock();
            {
                f.AppendLine("var type = kvp.Key;");
                f.AppendLine("var waiters = kvp.Value;");
                f.BeginStringBuilderAppend("message", false);
                {
                    f.StringBuilderAppendLine("  ▶ 缺失服务: {type.Name}");
                    f.StringBuilderAppendLine("    等待队列数量: {waiters.Count}");
                }
                f.EndStringBuilderAppend();
                f.AppendLine();

                f.AppendLine("foreach (var waiter in waiters)");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"var elapsed = {GlobalNames.DateTime}.Now.Ticks - waiter.RequestTicks;"
                    );
                    f.AppendLine(
                        $"var elapsedSeconds = {GlobalNames.TimeSpan}.FromTicks(elapsed).TotalSeconds;"
                    );
                    f.BeginStringBuilderAppend("message", false);
                    {
                        f.StringBuilderAppendLine("    • 请求者类型: {waiter.RequestorType}");
                        f.StringBuilderAppendLine("      等待时长: {elapsedSeconds:F1}秒");
                        f.StringBuilderAppendLine("      Scope 传递链: {waiter.ScopeChain}");
                        f.StringBuilderAppendLine("      依赖链条: {waiter.DependencyChain}");
                    }
                    f.EndStringBuilderAppend();
                }
                f.EndBlock();
            }
            f.EndBlock();
            f.AppendLine();

            f.PushError("message");
        }
        f.EndBlock();
    }
}
