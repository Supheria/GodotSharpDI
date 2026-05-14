using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Circular dependency detector
/// Uses an improved version of Tarjan's strongly connected components algorithm to detect and report circular dependencies
/// Supports member-level dependency tracking (especially WaitFor scenarios)
/// </summary>
internal sealed class CircularDependencyDetector
{
    private readonly ImmutableDictionary<ITypeSymbol, TypeNode> _serviceImplToNode;
    private readonly ImmutableDictionary<ITypeSymbol, ValidatedTypeInfo> _serviceProviders;

    // Tarjan algorithm state - using service types as nodes
    // Key format: service type (IServiceA, IServiceB etc.)
    private readonly Dictionary<ITypeSymbol, int> _indices;
    private readonly Dictionary<ITypeSymbol, int> _lowLinks;
    private readonly HashSet<ITypeSymbol> _onStack;
    private readonly Stack<ITypeSymbol> _stack;
    private int _index;

    // Detected cycles
    private readonly List<Cycle> _cycles;

    // Mapping from service type to the member providing that service (for generating detailed cycle paths)
    private readonly Dictionary<ITypeSymbol, ServiceMemberInfo> _serviceToMember;

    public CircularDependencyDetector(
        ImmutableDictionary<ITypeSymbol, TypeNode> serviceImplToNode,
        ImmutableDictionary<ITypeSymbol, ValidatedTypeInfo> serviceProviders
    )
    {
        _serviceImplToNode = serviceImplToNode;
        _serviceProviders = serviceProviders;

        _indices = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        _lowLinks = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        _onStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        _stack = new Stack<ITypeSymbol>();
        _index = 0;

        _cycles = new List<Cycle>();
        _serviceToMember = new Dictionary<ITypeSymbol, ServiceMemberInfo>(
            SymbolEqualityComparer.Default
        );

        BuildServiceToMemberMap();
    }

    /// <summary>
    /// Build mapping from service type to member information
    /// </summary>
    private void BuildServiceToMemberMap()
    {
        foreach (var node in _serviceImplToNode.Values)
        {
            foreach (var member in node.ValidatedTypeInfo.Members)
            {
                if (member.IsProvideMember)
                {
                    foreach (var exposedType in member.ExposedTypes)
                    {
                    // If multiple members provide the same service, use the first one
                    if (!_serviceToMember.ContainsKey(exposedType))
                        {
                            _serviceToMember[exposedType] = new ServiceMemberInfo(
                                node.ValidatedTypeInfo.Symbol,
                                member.Symbol.Name,
                                exposedType
                            );
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Detect all circular dependencies and return diagnostics
    /// </summary>
    public ImmutableArray<Diagnostic> DetectCircularDependencies()
    {
        // Run Tarjan algorithm on all service types
        foreach (var serviceType in _serviceToMember.Keys)
        {
            if (!_indices.ContainsKey(serviceType))
            {
                StrongConnect(serviceType);
            }
        }

        // Generate diagnostics from detected cycles
        return GenerateDiagnostics();
    }

    /// <summary>
    /// Core recursive function of Tarjan's strongly connected components algorithm
    /// Operates at the service type level, not at the Host type level
    /// </summary>
    private void StrongConnect(ITypeSymbol serviceType)
    {
        _indices[serviceType] = _index;
        _lowLinks[serviceType] = _index;
        _index++;
        _stack.Push(serviceType);
        _onStack.Add(serviceType);

        // Get the node and member providing this service
        if (!_serviceToMember.TryGetValue(serviceType, out var memberInfo))
        {
            PopAndCheckScc(serviceType);
            return;
        }

        if (!_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node))
        {
            PopAndCheckScc(serviceType);
            return;
        }

        // Find the member providing this service
        var providingMember = node.ValidatedTypeInfo.Members.FirstOrDefault(m =>
            m.Symbol.Name == memberInfo.MemberName && m.IsProvideMember
        );

        if (providingMember == null)
        {
            PopAndCheckScc(serviceType);
            return;
        }

        // Traverse this member's WaitFor dependencies
        foreach (var dependency in node.Dependencies)
        {
            // Only process WaitFor dependencies from this member
            if (
                dependency.Source == DependencySource.WaitForMember
                && dependency.SourceProvidedType != null
                && SymbolEqualityComparer.Default.Equals(dependency.SourceProvidedType, serviceType)
            )
            {
                var targetServiceType = dependency.TargetType;

                // Ensure the target service has a provider
                if (!_serviceToMember.ContainsKey(targetServiceType))
                    continue;

                if (!_indices.ContainsKey(targetServiceType))
                {
                    // Recursively visit unvisited dependencies
                    StrongConnect(targetServiceType);
                    _lowLinks[serviceType] = Math.Min(
                        _lowLinks[serviceType],
                        _lowLinks[targetServiceType]
                    );
                }
                else if (_onStack.Contains(targetServiceType))
                {
                    // Found back edge (circular dependency)
                    _lowLinks[serviceType] = Math.Min(
                        _lowLinks[serviceType],
                        _indices[targetServiceType]
                    );
                }
            }
        }

        // Check if this is the root of a strongly connected component
        if (_lowLinks[serviceType] == _indices[serviceType])
        {
            var component = new List<ITypeSymbol>();
            ITypeSymbol w;
            do
            {
                w = _stack.Pop();
                _onStack.Remove(w);
                component.Add(w);
            } while (!SymbolEqualityComparer.Default.Equals(w, serviceType));

            // If the strongly connected component contains multiple nodes, or has a self-loop, it's a circular dependency
            if (component.Count > 1 || HasEdgeToSelf(serviceType))
            {
                _cycles.Add(new Cycle(component));
            }
        }
    }

    /// <summary>
    /// Pop node and check if it forms an SCC root node.
    /// Used for early return paths in StrongConnect to ensure consistent stack state.
    /// </summary>
    private void PopAndCheckScc(ITypeSymbol serviceType)
    {
        // If current node is SCC root, pop the entire SCC
        if (_lowLinks[serviceType] == _indices[serviceType])
        {
            var component = new List<ITypeSymbol>();
            ITypeSymbol w;
            do
            {
                w = _stack.Pop();
                _onStack.Remove(w);
                component.Add(w);
            } while (!SymbolEqualityComparer.Default.Equals(w, serviceType));

            if (component.Count > 1 || HasEdgeToSelf(serviceType))
            {
                _cycles.Add(new Cycle(component));
            }
        }
    }

    /// <summary>
    /// Check if service type S has an edge pointing to itself (direct self-loop) in the WaitFor dependency graph
    /// i.e., a Provide member's WaitFor list contains its own exposed type
    /// Note: Indirect dependencies (A→B→A) are covered by Tarjan's multi-node SCC detection, no need to handle here
    /// </summary>
    private bool HasEdgeToSelf(ITypeSymbol serviceType)
    {
        if (!_serviceToMember.TryGetValue(serviceType, out var memberInfo)) return false;
        if (!_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node)) return false;

        foreach (var dep in node.Dependencies)
        {
            if (dep.Source != DependencySource.WaitForMember) continue;
            if (dep.SourceProvidedType == null) continue;

            // Only process WaitFor edges belonging to the current serviceType
            if (!SymbolEqualityComparer.Default.Equals(dep.SourceProvidedType, serviceType))
                continue;

            // Direct self-loop: Provide member waits for its own type to be injected
            // Example: [Provide(typeof(IServiceA), WaitFor=[nameof(_self)])] and _self type is IServiceA
            if (SymbolEqualityComparer.Default.Equals(dep.TargetType, serviceType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Generate diagnostics from detected cycles
    /// </summary>
    private ImmutableArray<Diagnostic> GenerateDiagnostics()
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var cycle in _cycles)
        {
            // Build cycle path (using service type names)
            var cyclePath = BuildCyclePath(cycle.Components);

            // Generate diagnostics for each service type in the cycle
            foreach (var serviceType in cycle.Components)
            {
                if (_serviceToMember.TryGetValue(serviceType, out var memberInfo))
                {
                    // Report error at the location of the Host class providing this service
                    if (_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node))
                    {
                        diagnostics.Add(
                            DiagnosticBuilder.Create(
                                DiagnosticDescriptors.CircularDependencyDetected,
                                node.ValidatedTypeInfo.Location,
                                cyclePath
                            )
                        );
                    }
                }
            }
        }

        return diagnostics.ToImmutable();
    }

    /// <summary>
    /// Build cycle dependency path string
    /// Uses service type names (not Host class names)
    /// </summary>
    private string BuildCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count == 1)
        {
            // Self-loop
            var serviceType = components[0];
            var serviceName = GetServiceDisplayName(serviceType);
            return $"{serviceName} -> {serviceName}";
        }

        // Reorder to display clear cycle path
        var orderedPath = OrderCyclePath(components);

        // Build path string using service type names
        var pathNames = orderedPath.Select(GetServiceDisplayName).ToList();

        // Add first node to the end to show complete cycle
        pathNames.Add(pathNames[0]);

        return string.Join(" -> ", pathNames);
    }

    /// <summary>
    /// Get display name for a service (prefer short name)
    /// </summary>
    private string GetServiceDisplayName(ITypeSymbol serviceType)
    {
        // Use short type name (e.g., "IServiceA" instead of "Test.IServiceA")
        return serviceType.Name;
    }

    /// <summary>
    /// Reorder components in the cycle to display clear dependency path
    /// </summary>
    private List<ITypeSymbol> OrderCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count <= 1)
            return components;

        // Build dependency graph within the cycle (between service types)
        var graph = new Dictionary<ITypeSymbol, List<ITypeSymbol>>(SymbolEqualityComparer.Default);
        var componentSet = new HashSet<ITypeSymbol>(components, SymbolEqualityComparer.Default);

        foreach (var serviceType in components)
        {
            graph[serviceType] = new List<ITypeSymbol>();

            if (
                _serviceToMember.TryGetValue(serviceType, out var memberInfo)
                && _serviceImplToNode.TryGetValue(memberInfo.HostType, out var node)
            )
            {
                // Find the service's WaitFor dependencies
                foreach (var dependency in node.Dependencies)
                {
                    if (
                        dependency.Source == DependencySource.WaitForMember
                        && dependency.SourceProvidedType != null
                        && SymbolEqualityComparer.Default.Equals(
                            dependency.SourceProvidedType,
                            serviceType
                        )
                        && componentSet.Contains(dependency.TargetType)
                    )
                    {
                        graph[serviceType].Add(dependency.TargetType);
                    }
                }
            }
        }

        // Start building path from the first component
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var path = new List<ITypeSymbol>();
        BuildOrderedPath(components[0], graph, componentSet, visited, path);

        return path.Count > 0 ? path : components;
    }

    /// <summary>
    /// Recursively build ordered cycle path.
    /// Fix: When a node has multiple outgoing edges and the preferred node is already visited,
    /// try remaining unvisited edges to avoid path truncation due to FirstOrDefault selecting visited node.
    /// </summary>
    private void BuildOrderedPath(
        ITypeSymbol current,
        Dictionary<ITypeSymbol, List<ITypeSymbol>> graph,
        HashSet<ITypeSymbol> componentSet,
        HashSet<ITypeSymbol> visited,
        List<ITypeSymbol> path
    )
    {
        if (visited.Contains(current))
            return;

        visited.Add(current);
        path.Add(current);

        // Prefer unvisited in-cycle nodes to avoid path truncation
        var nextInCycle = graph[current]
            .FirstOrDefault(dep => componentSet.Contains(dep) && !visited.Contains(dep));

        if (nextInCycle != null)
        {
            BuildOrderedPath(nextInCycle, graph, componentSet, visited, path);
        }
    }

    /// <summary>
    /// Represents a detected circular dependency
    /// </summary>
    private sealed class Cycle
    {
        public List<ITypeSymbol> Components { get; }

        public Cycle(List<ITypeSymbol> components)
        {
            Components = components;
        }
    }

    /// <summary>
    /// Service member information - Records which member of which Host provides which service
    /// </summary>
    private sealed class ServiceMemberInfo
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
