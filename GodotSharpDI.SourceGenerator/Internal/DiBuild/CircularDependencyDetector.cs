using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Detects circular dependencies formed by WaitFor edges between service types.
///
/// Architecture:
/// 1. Build adjacency list (service type → dependent service types) from WaitFor edges
/// 2. Run TarjanSCC to find strongly connected components
/// 3. Filter: keep SCCs with &gt;1 node, or single-node with self-loop
/// 4. Use CyclePathBuilder to format human-readable cycle paths
/// 5. Generate Diagnostic for each cycle
/// </summary>
internal sealed class CircularDependencyDetector
{
    private readonly ImmutableDictionary<ITypeSymbol, TypeNode> _serviceImplToNode;
    private readonly ImmutableDictionary<ITypeSymbol, ValidatedTypeInfo> _serviceProviders;

    public CircularDependencyDetector(
        ImmutableDictionary<ITypeSymbol, TypeNode> serviceImplToNode,
        ImmutableDictionary<ITypeSymbol, ValidatedTypeInfo> serviceProviders)
    {
        _serviceImplToNode = serviceImplToNode;
        _serviceProviders = serviceProviders;
    }

    /// <summary>
    /// Detect all circular dependencies and return diagnostics.
    /// </summary>
    public ImmutableArray<Diagnostic> DetectCircularDependencies()
    {
        var serviceToMember = BuildServiceToMemberMap();
        var graph = BuildAdjacencyList(serviceToMember);
        var sccs = TarjanSCC<ITypeSymbol>.Detect(graph, SymbolEqualityComparer.Default);

        var pathBuilder = new CyclePathBuilder(_serviceImplToNode, serviceToMember);
        var cycles = FilterCycles(sccs, pathBuilder);

        return GenerateDiagnostics(cycles, pathBuilder);
    }

    /// <summary>
    /// Build mapping from service type to the member providing that service.
    /// </summary>
    private Dictionary<ITypeSymbol, CyclePathBuilder.ServiceMemberInfo> BuildServiceToMemberMap()
    {
        var map = new Dictionary<ITypeSymbol, CyclePathBuilder.ServiceMemberInfo>(
            SymbolEqualityComparer.Default);

        foreach (var node in _serviceImplToNode.Values)
        {
            foreach (var member in node.ValidatedTypeInfo.Members)
            {
                if (!member.IsProvideMember)
                    continue;

                foreach (var exposedType in member.ExposedTypes)
                {
                    if (!map.ContainsKey(exposedType))
                    {
                        map[exposedType] = new CyclePathBuilder.ServiceMemberInfo(
                            node.ValidatedTypeInfo.Symbol,
                            member.Symbol.Name,
                            exposedType);
                    }
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Build adjacency list: service type → list of service types it waits for via WaitFor.
    /// Only includes edges where both source and target have registered providers.
    /// </summary>
    private IReadOnlyDictionary<ITypeSymbol, IEnumerable<ITypeSymbol>> BuildAdjacencyList(
        Dictionary<ITypeSymbol, CyclePathBuilder.ServiceMemberInfo> serviceToMember)
    {
        var graph = new Dictionary<ITypeSymbol, IEnumerable<ITypeSymbol>>(
            SymbolEqualityComparer.Default);

        foreach (var serviceType in serviceToMember.Keys)
        {
            if (!serviceToMember.TryGetValue(serviceType, out var memberInfo))
                continue;
            if (!_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node))
                continue;

            var neighbors = new List<ITypeSymbol>();
            foreach (var dep in node.Dependencies)
            {
                if (dep.Source != DependencySource.WaitForMember)
                    continue;
                if (dep.SourceProvidedType == null)
                    continue;
                if (!SymbolEqualityComparer.Default.Equals(dep.SourceProvidedType, serviceType))
                    continue;
                if (!serviceToMember.ContainsKey(dep.TargetType))
                    continue;

                neighbors.Add(dep.TargetType);
            }

            if (neighbors.Count > 0)
                graph[serviceType] = neighbors;
        }

        return graph;
    }

    /// <summary>
    /// Filter SCCs: keep multi-node SCCs and single-node SCCs with self-loops.
    /// </summary>
    private static List<List<ITypeSymbol>> FilterCycles(
        List<List<ITypeSymbol>> sccs,
        CyclePathBuilder pathBuilder)
    {
        var cycles = new List<List<ITypeSymbol>>();
        foreach (var scc in sccs)
        {
            if (scc.Count > 1)
            {
                cycles.Add(scc);
            }
            else if (scc.Count == 1 && pathBuilder.HasSelfLoop(scc[0]))
            {
                cycles.Add(scc);
            }
        }
        return cycles;
    }

    /// <summary>
    /// Generate diagnostics from detected cycles.
    /// </summary>
    private ImmutableArray<Diagnostic> GenerateDiagnostics(
        List<List<ITypeSymbol>> cycles,
        CyclePathBuilder pathBuilder)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var cycle in cycles)
        {
            var cyclePath = pathBuilder.BuildCyclePath(cycle);

            foreach (var serviceType in cycle)
            {
                if (!_serviceProviders.TryGetValue(serviceType, out var provider))
                    continue;

                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.CircularDependencyDetected,
                        provider.Location,
                        cyclePath));
            }
        }

        return diagnostics.ToImmutable();
    }
}
