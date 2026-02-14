using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 循环依赖检测器
/// 使用 Tarjan 强连通分量算法的改进版本来检测和报告循环依赖
/// 支持成员级别的依赖追踪（特别是WaitFor场景）
/// </summary>
internal sealed class CircularDependencyDetector
{
    private readonly ImmutableDictionary<ITypeSymbol, TypeNode> _serviceImplToNode;
    private readonly ImmutableDictionary<ITypeSymbol, ValidatedTypeInfo> _serviceProviders;

    // Tarjan 算法状态 - 使用服务类型作为节点
    // 键的格式：服务类型 (IServiceA, IServiceB etc.)
    private readonly Dictionary<ITypeSymbol, int> _indices;
    private readonly Dictionary<ITypeSymbol, int> _lowLinks;
    private readonly HashSet<ITypeSymbol> _onStack;
    private readonly Stack<ITypeSymbol> _stack;
    private int _index;

    // 检测到的循环
    private readonly List<Cycle> _cycles;

    // 服务类型到提供该服务的成员的映射（用于生成详细的循环路径）
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
    /// 构建服务类型到成员信息的映射
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
                        // 如果有多个成员提供同一服务，使用第一个
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
    /// 检测所有循环依赖并返回诊断信息
    /// </summary>
    public ImmutableArray<Diagnostic> DetectCircularDependencies()
    {
        // 对所有服务类型运行 Tarjan 算法
        foreach (var serviceType in _serviceToMember.Keys)
        {
            if (!_indices.ContainsKey(serviceType))
            {
                StrongConnect(serviceType);
            }
        }

        // 从检测到的循环生成诊断信息
        return GenerateDiagnostics();
    }

    /// <summary>
    /// Tarjan 强连通分量算法的核心递归函数
    /// 在服务类型层面运行，而不是在Host类型层面
    /// </summary>
    private void StrongConnect(ITypeSymbol serviceType)
    {
        _indices[serviceType] = _index;
        _lowLinks[serviceType] = _index;
        _index++;
        _stack.Push(serviceType);
        _onStack.Add(serviceType);

        // 获取提供该服务的节点和成员
        if (!_serviceToMember.TryGetValue(serviceType, out var memberInfo))
            return;

        if (!_serviceImplToNode.TryGetValue(memberInfo.HostType, out var node))
            return;

        // 找到提供这个服务的成员
        var providingMember = node.ValidatedTypeInfo.Members.FirstOrDefault(m =>
            m.Symbol.Name == memberInfo.MemberName && m.IsProvideMember
        );

        if (providingMember == null)
            return;

        // 遍历该成员的WaitFor依赖
        foreach (var dependency in node.Dependencies)
        {
            // 只处理来自该成员的WaitFor依赖
            if (
                dependency.Source == DependencySource.WaitForMember
                && dependency.SourceProvidedType != null
                && SymbolEqualityComparer.Default.Equals(dependency.SourceProvidedType, serviceType)
            )
            {
                var targetServiceType = dependency.TargetType;

                // 确保目标服务有提供者
                if (!_serviceToMember.ContainsKey(targetServiceType))
                    continue;

                if (!_indices.ContainsKey(targetServiceType))
                {
                    // 递归访问未访问的依赖
                    StrongConnect(targetServiceType);
                    _lowLinks[serviceType] = Math.Min(
                        _lowLinks[serviceType],
                        _lowLinks[targetServiceType]
                    );
                }
                else if (_onStack.Contains(targetServiceType))
                {
                    // 发现后向边（循环依赖）
                    _lowLinks[serviceType] = Math.Min(
                        _lowLinks[serviceType],
                        _indices[targetServiceType]
                    );
                }
            }
        }

        // 检查是否是强连通分量的根
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

            // 如果强连通分量包含多个节点，或有自环，则是循环依赖
            if (component.Count > 1 || HasSelfLoop(serviceType, node))
            {
                _cycles.Add(new Cycle(component));
            }
        }
    }

    /// <summary>
    /// 检查服务是否有自环（通过WaitFor依赖自己）
    /// </summary>
    private bool HasSelfLoop(ITypeSymbol serviceType, TypeNode node)
    {
        foreach (var dependency in node.Dependencies)
        {
            if (
                dependency.Source == DependencySource.WaitForMember
                && dependency.SourceProvidedType != null
                && SymbolEqualityComparer.Default.Equals(dependency.SourceProvidedType, serviceType)
                && SymbolEqualityComparer.Default.Equals(dependency.TargetType, serviceType)
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从检测到的循环生成诊断信息
    /// </summary>
    private ImmutableArray<Diagnostic> GenerateDiagnostics()
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var cycle in _cycles)
        {
            // 构建循环路径（使用服务类型名称）
            var cyclePath = BuildCyclePath(cycle.Components);

            // 为循环中的每个服务类型生成诊断
            foreach (var serviceType in cycle.Components)
            {
                if (_serviceToMember.TryGetValue(serviceType, out var memberInfo))
                {
                    // 在提供该服务的Host类的位置报告错误
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
    /// 构建循环依赖路径字符串
    /// 使用服务类型名称（而不是Host类名）
    /// </summary>
    private string BuildCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count == 1)
        {
            // 自环
            var serviceType = components[0];
            var serviceName = GetServiceDisplayName(serviceType);
            return $"{serviceName} -> {serviceName}";
        }

        // 重新排序以显示清晰的循环路径
        var orderedPath = OrderCyclePath(components);

        // 构建路径字符串，使用服务类型名称
        var pathNames = orderedPath.Select(GetServiceDisplayName).ToList();

        // 添加第一个节点到末尾以显示完整循环
        pathNames.Add(pathNames[0]);

        return string.Join(" -> ", pathNames);
    }

    /// <summary>
    /// 获取服务的显示名称（优先使用简短名称）
    /// </summary>
    private string GetServiceDisplayName(ITypeSymbol serviceType)
    {
        // 使用简短的类型名称（例如 "IServiceA" 而不是 "Test.IServiceA"）
        return serviceType.Name;
    }

    /// <summary>
    /// 重新排序循环中的组件以显示清晰的依赖路径
    /// </summary>
    private List<ITypeSymbol> OrderCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count <= 1)
            return components;

        // 构建循环内的依赖图（服务类型之间）
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
                // 查找该服务的WaitFor依赖
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

        // 从第一个组件开始构建路径
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var path = new List<ITypeSymbol>();
        BuildOrderedPath(components[0], graph, componentSet, visited, path);

        return path.Count > 0 ? path : components;
    }

    /// <summary>
    /// 递归构建排序后的循环路径
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

        // 找到循环中的下一个节点
        var nextInCycle = graph[current].FirstOrDefault(dep => componentSet.Contains(dep));

        if (nextInCycle != null && !visited.Contains(nextInCycle))
        {
            BuildOrderedPath(nextInCycle, graph, componentSet, visited, path);
        }
    }

    /// <summary>
    /// 表示一个检测到的循环依赖
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
    /// 服务成员信息 - 记录哪个Host的哪个成员提供了哪个服务
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
