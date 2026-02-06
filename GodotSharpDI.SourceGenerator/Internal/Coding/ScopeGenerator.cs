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

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            GenerateStaticCollections(f, node, graph);
            f.AppendLine();

            GenerateInstanceFields(f);
            f.AppendLine();

            GenerateInstantiateScopeSingletons(f, node, graph);
            f.AppendLine();

            GenerateDisposeScopeSingletons(f, node.ValidatedTypeInfo);
            f.AppendLine();

            GenerateDependencyWaitInfoStruct(f);
            f.AppendLine();

            GenerateDependencyMonitoringMethods(f, node.ValidatedTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Scope.g.cs", f.ToString());
    }

    private static HashSet<ITypeSymbol> CollectServiceTypes(ScopeNode node, DiGraph graph)
    {
        var singletonServiceTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // 从 Instantiate 的服务中收集
        foreach (var serviceType in node.InstantiateServices)
        {
            if (graph.ServiceNodeMap.TryGetValue(serviceType, out var serviceNode))
            {
                foreach (var exposedType in serviceNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
        }

        // 从 Expect 的 Host 中收集
        foreach (var hostType in node.ExpectHosts)
        {
            if (graph.HostNodeMap.TryGetValue(hostType, out var hostNode))
            {
                // 添加 Host 提供的所有服务类型
                foreach (var exposedType in hostNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
            else if (graph.HostAndUserNodeMap.TryGetValue(hostType, out var hostAndUserNode))
            {
                // 添加 HostAndUser 提供的所有服务类型
                foreach (var exposedType in hostAndUserNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
        }

        return singletonServiceTypes;
    }

    private static void GenerateStaticCollections(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        // ServiceTypes
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private static readonly {GlobalNames.HashSet}<{GlobalNames.Type}> ServiceTypes = new()"
        );
        f.BeginBlock();
        {
            var singletonServiceTypes = CollectServiceTypes(node, graph);
            foreach (var serviceType in singletonServiceTypes)
            {
                f.AppendLine($"typeof({serviceType.ToFullyQualifiedName()}),");
            }
        }
        f.EndBlock(";");
    }

    private static void GenerateInstanceFields(CodeFormatter f)
    {
        // _services
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Object}> _services = new();"
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

    private static void GenerateInstantiateScopeSingletons(
        CodeFormatter f,
        ScopeNode node,
        DiGraph graph
    )
    {
        // InstantiateScopeSingletons
        f.AppendHiddenMethodCommentAndAttribute("实例化所有 Scope 约束的单例服务");
        f.AppendLine("private void InstantiateScopeSingletons()");
        f.BeginBlock();
        {
            foreach (var serviceType in node.InstantiateServices)
            {
                if (!graph.ServiceNodeMap.TryGetValue(serviceType, out var serviceNode))
                {
                    continue;
                }

                var simpleServiceName = serviceType.ToFullyQualifiedName();

                f.AppendLine($"{simpleServiceName}.CreateService(");
                f.BeginLevel();
                {
                    f.AppendLine("this,");
                    f.AppendLine("(instance, scope) =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"if (instance is {GlobalNames.IDisposable} disposable)");
                        f.BeginBlock();
                        {
                            f.AppendLine("_disposableSingletons.Add(disposable);");
                        }
                        f.EndBlock();

                        foreach (var exposedType in serviceNode.ProvidedServices)
                        {
                            f.AppendLine(
                                $"scope.ProvideService(({exposedType.ToFullyQualifiedName()})instance);"
                            );
                        }
                    }
                    f.EndBlock();
                }
                f.EndLevel();
                f.AppendLine(");");
            }
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
            f.AppendLine("_services.Clear();");
        }
        f.EndBlock();
    }

    private static void GenerateDependencyWaitInfoStruct(CodeFormatter f)
    {
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine("private struct DependencyWaitInfo");
        f.BeginBlock();
        {
            f.AppendLine($"public {GlobalNames.Action}<{GlobalNames.Object}> Callback;");
            f.AppendLine($"public {GlobalNames.Long} RequestTicks;");
            f.AppendLine($"public {GlobalNames.String} RequestorType;");
            f.AppendLine($"public {GlobalNames.String} ScopeChain;");
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
