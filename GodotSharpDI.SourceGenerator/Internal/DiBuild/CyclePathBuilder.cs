using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Builds human-readable cycle path strings from detected SCCs.
/// </summary>
internal sealed class CyclePathBuilder
{
    private readonly ImmutableDictionary<ITypeSymbol, TypeNode> _serviceImplToNode;
    private readonly Dictionary<ITypeSymbol, ServiceMemberInfo> _serviceToMember;

    public CyclePathBuilder(
        ImmutableDictionary<ITypeSymbol, TypeNode> serviceImplToNode,
        Dictionary<ITypeSymbol, ServiceMemberInfo> serviceToMember)
    {
        _serviceImplToNode = serviceImplToNode;
        _serviceToMember = serviceToMember;
    }

    /// <summary>
    /// Build a readable cycle path string, e.g. "IServiceA -> IServiceB -> IServiceA".
    /// </summary>
    public string BuildCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count == 1)
        {
            var name = components[0].Name;
            return $"{name} -> {name}";
        }

        var ordered = OrderCyclePath(components);
        var names = ordered.Select(c => c.Name).ToList();
        names.Add(names[0]);
        return string.Join(" -> ", names);
    }

    /// <summary>
    /// Check if a service type has a direct self-loop (Provide waits for its own exposed type).
    /// </summary>
    public bool HasSelfLoop(ITypeSymbol serviceType)
    {
        if (!_serviceToMember.TryGetValue(serviceType, out var memberInfo))
            return false;
        if (!_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node))
            return false;

        return node.Dependencies.Any(dep =>
            dep.Source == DependencySource.WaitForMember
            && dep.SourceProvidedType != null
            && SymbolEqualityComparer.Default.Equals(dep.SourceProvidedType, serviceType)
            && SymbolEqualityComparer.Default.Equals(dep.TargetType, serviceType));
    }

    /// <summary>
    /// Reorder components in the cycle to display a clear dependency path.
    /// </summary>
    private List<ITypeSymbol> OrderCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count <= 1)
            return components;

        var graph = BuildInternalGraph(components);
        var componentSet = new HashSet<ITypeSymbol>(components, SymbolEqualityComparer.Default);
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var path = new List<ITypeSymbol>();

        BuildOrderedPath(components[0], graph, componentSet, visited, path);
        return path.Count > 0 ? path : components;
    }

    private Dictionary<ITypeSymbol, List<ITypeSymbol>> BuildInternalGraph(
        List<ITypeSymbol> components)
    {
        var graph = new Dictionary<ITypeSymbol, List<ITypeSymbol>>(SymbolEqualityComparer.Default);
        var componentSet = new HashSet<ITypeSymbol>(components, SymbolEqualityComparer.Default);

        foreach (var serviceType in components)
        {
            graph[serviceType] = new List<ITypeSymbol>();

            if (!_serviceToMember.TryGetValue(serviceType, out var memberInfo))
                continue;
            if (!_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node))
                continue;

            foreach (var dep in node.Dependencies)
            {
                if (dep.Source == DependencySource.WaitForMember
                    && dep.SourceProvidedType != null
                    && SymbolEqualityComparer.Default.Equals(dep.SourceProvidedType, serviceType)
                    && componentSet.Contains(dep.TargetType))
                {
                    graph[serviceType].Add(dep.TargetType);
                }
            }
        }

        return graph;
    }

    private static void BuildOrderedPath(
        ITypeSymbol current,
        Dictionary<ITypeSymbol, List<ITypeSymbol>> graph,
        HashSet<ITypeSymbol> componentSet,
        HashSet<ITypeSymbol> visited,
        List<ITypeSymbol> path)
    {
        if (visited.Contains(current))
            return;

        visited.Add(current);
        path.Add(current);

        var next = graph[current]
            .FirstOrDefault(dep => componentSet.Contains(dep) && !visited.Contains(dep));

        if (next != null)
            BuildOrderedPath(next, graph, componentSet, visited, path);
    }

    /// <summary>
    /// Service member information — records which member of which Host provides which service.
    /// </summary>
    internal sealed class ServiceMemberInfo
    {
        public ITypeSymbol HostType { get; }
        public string MemberName { get; }
        public ITypeSymbol ServiceType { get; }

        public ServiceMemberInfo(ITypeSymbol hostType, string memberName, ITypeSymbol serviceType)
        {
            HostType = hostType;
            MemberName = memberName;
            ServiceType = serviceType;
        }
    }
}
