using System.Collections.Generic;
using System.Text;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope code generator
/// </summary>
internal static class ScopeGenerator
{
    public static void Generate(SourceProductionContext context, ScopeNode node, DiGraph graph)
    {
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        ScopeInterfaceGenerator.GenerateInterface(context, node);

        // Generate Scope specific code
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
            GenerateStaticCollections(f);
            f.AppendLine();

            GenerateInstanceFields(f);
            f.AppendLine();

            GenerateStaticMethods(f, node, graph);
            f.AppendLine();

            GenerateDependencyMonitoringMethods(f, node.ValidatedTypeInfo);
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Scope.g.cs", f.ToString());
    }

    private static void GenerateStaticCollections(CodeFormatter f)
    {
        // ServiceImplementationMap
        f.AppendHiddenMemberCommentAndAttribute("Service type mapping table: exposed type -> implementation type");
        f.AppendLine(
            $"private static readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Type}> ServiceImplementationMap = CreateServiceImplementationMap();"
        );
    }

    private static void GenerateInstanceFields(CodeFormatter f)
    {
        // ServiceCache
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.ServiceCacheEntry}> ServiceCache = CreateServiceCache();"
        );
        f.AppendLine();

        // _waiters
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.List}<{GlobalNames.DependencyWaitInfo}>> _waiters = new();"
        );
        f.AppendLine();

        // Deadlock detector (DEBUG mode only)
        f.BeginDebugRegion();
        {
            f.AppendHiddenMemberCommentAndAttribute("Runtime WaitFor deadlock detector (DEBUG only)");
            f.AppendLine(
                $"private readonly {GlobalNames.DeadlockDetector} _deadlockDetector = CreateDeadlockDetector();"
            );
        }
        f.EndDebugRegion();
        f.AppendLine();

        // _dependencyCheckTimer
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.GodotTimer}? _dependencyCheckTimer;");
    }

    private static void GenerateStaticMethods(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        // ServiceCache uses "implementation type (TImpl)" as key, not the exposed interface type.
        // ServiceImplementationMap is responsible for mapping from exposed type (TExposed) to implementation type (TImpl).
        // Lookup flow: ResolveDependency<TExposed> → ServiceImplementationMap[TExposed] → implType
        //         → ServiceCache[implType] → instance
        // When exposed type and implementation type are the same (e.g., Host exposes itself), both point to the same Type key, behavior is correct.
        // Collect all implementation types needed by this Scope
        var implementationTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Collect the mapping from service exposed type to implementation type for this Scope
        var scopeServiceImplMap = new Dictionary<INamedTypeSymbol, INamedTypeSymbol>(
            SymbolEqualityComparer.Default
        );

        // Traverse all Hosts in this Scope
        foreach (var hostType in node.ExpectHosts)
        {
            if (graph.HostNodeMap.TryGetValue(hostType, out var hostNode))
            {
                // Collect service mappings provided by this Host
                foreach (var kvp in hostNode.ServiceImplementationMap)
                {
                    var exposedType = kvp.Key;
                    var implementationType = kvp.Value;

                    // Add to Scope's service mapping
                    scopeServiceImplMap[exposedType] = implementationType;

                    // Add implementation type to set
                    implementationTypes.Add(implementationType);
                }
            }
        }

        // CreateServiceCache - Use implementation type as key
        f.AppendHiddenMethodCommentAndAttribute("Initialize all service caches (using implementation type as key)");
        f.AppendLine(
            $"private static {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.ServiceCacheEntry}> CreateServiceCache()"
        );
        f.BeginBlock();
        {
            f.AppendLine(
                $"var cache = new {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.ServiceCacheEntry}>();"
            );
            f.AppendLine();

            foreach (var implType in implementationTypes)
            {
                f.AppendLine($"cache[typeof({implType.ToFullyQualifiedName()})] = new();");
            }
            f.AppendLine();

            f.AppendLine("return cache;");
        }
        f.EndBlock();

        // CreateServiceImplementationMap - Mapping from exposed type to implementation type
        f.AppendHiddenMethodCommentAndAttribute("Create mapping table from service exposed type to implementation type");
        f.AppendLine(
            $"private static {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Type}> CreateServiceImplementationMap()"
        );
        f.BeginBlock();
        {
            f.AppendLine(
                $"var map = new {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Type}>();"
            );
            f.AppendLine();

            foreach (var kvp in scopeServiceImplMap)
            {
                var exposedType = kvp.Key;
                var implType = kvp.Value;
                f.AppendLine(
                    $"map[typeof({exposedType.ToFullyQualifiedName()})] = typeof({implType.ToFullyQualifiedName()});"
                );
            }
            f.AppendLine();

            f.AppendLine("return map;");
        }
        f.EndBlock();

        // CreateDeadlockDetector - Initialize deadlock detector with service-to-provider mappings (DEBUG only)
        f.BeginDebugRegion();
        {
            f.AppendHiddenMethodCommentAndAttribute("Initialize deadlock detector with service-to-provider mappings");
            f.AppendLine(
                $"private static {GlobalNames.DeadlockDetector} CreateDeadlockDetector()"
            );
            f.BeginBlock();
            {
                f.AppendLine($"var detector = new {GlobalNames.DeadlockDetector}();");
                f.AppendLine();

                foreach (var kvp in scopeServiceImplMap)
                {
                    var exposedType = kvp.Key;
                    var implType = kvp.Value;
                    f.AppendLine(
                        $"detector.RegisterServiceProvider(\"{implType.Name}\", \"{exposedType.Name}\");"
                    );
                }

                f.AppendLine();
                f.AppendLine("return detector;");
            }
            f.EndBlock();
        }
        f.EndDebugRegion();
    }

    private static void GenerateDependencyMonitoringMethods(
        CodeFormatter f,
        ValidatedTypeInfo validatedType
    )
    {
        GenerateStartDependencyMonitoring(f);
        f.AppendLine();

        GenerateStopDependencyMonitoring(f);
        f.AppendLine();

        GenerateCheckPendingDependencies(f, validatedType);
        f.AppendLine();

        GenerateReportUnresolvedDependencies(f, validatedType);
    }

    private static void GenerateStartDependencyMonitoring(CodeFormatter f)
    {
        f.AppendHiddenMethodCommentAndAttribute("Start dependency monitoring (debug only)");
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
    }

    private static void GenerateStopDependencyMonitoring(CodeFormatter f)
    {
        f.AppendHiddenMethodCommentAndAttribute("Stop dependency monitoring (debug only)");
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
    }

    private static void GenerateCheckPendingDependencies(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendHiddenMethodCommentAndAttribute("Check pending dependencies (called periodically in debug)");
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
                                f.StringBuilderAppendLine(GeneratedStrings.WarnInjectionTimeout);
                                f.StringBuilderAppendLine(
                                    $"{GeneratedStrings.LabelCurrentScope}{validatedType.Symbol.Name}"
                                );
                                f.StringBuilderAppendLine($"{GeneratedStrings.LabelServiceType}{{type.Name}}");
                                f.StringBuilderAppendLine($"{GeneratedStrings.LabelRequestor}{{waiter.RequestorType}}");
                                f.StringBuilderAppendLine($"{GeneratedStrings.LabelElapsed}{{elapsedSeconds:F1}}s");
                                f.StringBuilderAppendLine($"{GeneratedStrings.LabelScopeChain}{{waiter.ScopeChain}}");
                                f.StringBuilderAppendLine($"{GeneratedStrings.LabelDependency}{{waiter.DependencyChain}}");
                            }
                            f.EndStringBuilderAppend();
                            f.AppendLine();

                            f.PrintError("message.ToString()");
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
    }

    private static void GenerateReportUnresolvedDependencies(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        f.AppendHiddenMethodCommentAndAttribute("Report all unresolved dependencies (debug only)");
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
                    string.Format(GeneratedStrings.WarnUnresolvedDependencies, validatedType.Symbol.Name)
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
                    f.StringBuilderAppendLine($"  > Missing service: {{type.Name}}");
                    f.StringBuilderAppendLine($"{GeneratedStrings.LabelWaiters}{{waiters.Count}}");
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
                        f.StringBuilderAppendLine($"    {GeneratedStrings.LabelRequestor}{{waiter.RequestorType}}");
                        f.StringBuilderAppendLine($"    {GeneratedStrings.LabelElapsed}{{elapsedSeconds:F1}}s");
                        f.StringBuilderAppendLine($"    {GeneratedStrings.LabelScopeChain}{{waiter.ScopeChain}}");
                        f.StringBuilderAppendLine($"    {GeneratedStrings.LabelDependency}{{waiter.DependencyChain}}");
                    }
                    f.EndStringBuilderAppend();
                }
                f.EndBlock();
            }
            f.EndBlock();
            f.AppendLine();

            f.PrintError("message.ToString()");
        }
        f.EndBlock();
    }

}
