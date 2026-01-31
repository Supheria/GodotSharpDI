using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharpDI.Generator.Internal.Data.TypeInfo;

namespace GodotSharpDI.Generator.Internal.DiBuild;

/// <summary>
/// 循环依赖检测器
/// 使用 Tarjan 强连通分量算法的改进版本来检测和报告循环依赖
/// </summary>
internal sealed class CircularDependencyDetector
{
    private readonly Dictionary<ITypeSymbol, TypeNode> _serviceImplToNode;
    private readonly Dictionary<ITypeSymbol, TypeInfo> _serviceProviders;

    // Tarjan 算法状态
    private readonly Dictionary<ITypeSymbol, int> _indices;
    private readonly Dictionary<ITypeSymbol, int> _lowLinks;
    private readonly HashSet<ITypeSymbol> _onStack;
    private readonly Stack<ITypeSymbol> _stack;
    private int _index;

    // 检测到的循环
    private readonly List<Cycle> _cycles;

    public CircularDependencyDetector(
        Dictionary<ITypeSymbol, TypeNode> serviceImplToNode,
        Dictionary<ITypeSymbol, TypeInfo> serviceProviders
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
    }

    /// <summary>
    /// 检测所有循环依赖并返回诊断信息
    /// </summary>
    public ImmutableArray<Diagnostic> DetectCircularDependencies()
    {
        // 对所有服务节点运行 Tarjan 算法
        foreach (var node in _serviceImplToNode.Values)
        {
            if (!_indices.ContainsKey(node.TypeInfo.Symbol))
            {
                StrongConnect(node.TypeInfo.Symbol);
            }
        }

        // 从检测到的循环生成诊断信息
        return GenerateDiagnostics();
    }

    /// <summary>
    /// Tarjan 强连通分量算法的核心递归函数
    /// </summary>
    private void StrongConnect(ITypeSymbol typeSymbol)
    {
        // 初始化节点
        _indices[typeSymbol] = _index;
        _lowLinks[typeSymbol] = _index;
        _index++;
        _stack.Push(typeSymbol);
        _onStack.Add(typeSymbol);

        // 考虑所有依赖
        if (_serviceImplToNode.TryGetValue(typeSymbol, out var node))
        {
            if (node.TypeInfo.Constructor != null)
            {
                foreach (var param in node.TypeInfo.Constructor.Parameters)
                {
                    // 获取参数类型的提供者
                    if (_serviceProviders.TryGetValue(param.Type, out var provider))
                    {
                        var dependencySymbol = provider.Symbol;

                        if (!_indices.ContainsKey(dependencySymbol))
                        {
                            // 依赖未被访问，递归访问
                            StrongConnect(dependencySymbol);
                            _lowLinks[typeSymbol] = Math.Min(
                                _lowLinks[typeSymbol],
                                _lowLinks[dependencySymbol]
                            );
                        }
                        else if (_onStack.Contains(dependencySymbol))
                        {
                            // 依赖在栈中，说明有环
                            _lowLinks[typeSymbol] = Math.Min(
                                _lowLinks[typeSymbol],
                                _indices[dependencySymbol]
                            );
                        }
                    }
                }
            }
        }

        // 如果是强连通分量的根节点
        if (_lowLinks[typeSymbol] == _indices[typeSymbol])
        {
            var component = new List<ITypeSymbol>();
            ITypeSymbol w;
            do
            {
                w = _stack.Pop();
                _onStack.Remove(w);
                component.Add(w);
            } while (!SymbolEqualityComparer.Default.Equals(w, typeSymbol));

            // 如果强连通分量大小 > 1，说明有循环
            if (component.Count > 1)
            {
                _cycles.Add(new Cycle(component));
            }
            // 如果大小 = 1，检查是否有自环
            else if (component.Count == 1)
            {
                var singleNode = component[0];
                if (HasSelfLoop(singleNode))
                {
                    _cycles.Add(new Cycle(component));
                }
            }
        }
    }

    /// <summary>
    /// 检查节点是否有自环（依赖自己）
    /// </summary>
    private bool HasSelfLoop(ITypeSymbol typeSymbol)
    {
        if (!_serviceImplToNode.TryGetValue(typeSymbol, out var node))
            return false;

        if (node.TypeInfo.Constructor == null)
            return false;

        foreach (var param in node.TypeInfo.Constructor.Parameters)
        {
            if (_serviceProviders.TryGetValue(param.Type, out var provider))
            {
                if (SymbolEqualityComparer.Default.Equals(provider.Symbol, typeSymbol))
                {
                    return true;
                }
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
            // 构建循环路径
            var cyclePath = BuildCyclePath(cycle.Components);

            // 找到循环中的最佳报告位置（第一个节点）
            var firstNode = _serviceImplToNode[cycle.Components[0]];

            diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.CircularDependencyDetected,
                    firstNode.TypeInfo.Location,
                    cyclePath
                )
            );
        }

        return diagnostics.ToImmutable();
    }

    /// <summary>
    /// 构建循环依赖路径字符串
    /// 确保路径是完整且易于理解的
    /// </summary>
    private string BuildCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count == 1)
        {
            // 自环
            var typeName = components[0].ToDisplayString();
            return $"{typeName} -> {typeName}";
        }

        // 重新排序以显示清晰的循环路径
        var orderedPath = OrderCyclePath(components);

        // 构建路径字符串
        var pathNames = orderedPath.Select(t => t.ToDisplayString()).ToList();

        // 添加第一个节点到末尾以显示完整循环
        pathNames.Add(pathNames[0]);

        return string.Join(" -> ", pathNames);
    }

    /// <summary>
    /// 重新排序循环中的组件以显示清晰的依赖路径
    /// </summary>
    private List<ITypeSymbol> OrderCyclePath(List<ITypeSymbol> components)
    {
        if (components.Count <= 2)
            return components;

        // 构建依赖关系图（只在循环组件内）
        var componentSet = new HashSet<ITypeSymbol>(components, SymbolEqualityComparer.Default);
        var graph = new Dictionary<ITypeSymbol, List<ITypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var component in components)
        {
            graph[component] = new List<ITypeSymbol>();

            if (_serviceImplToNode.TryGetValue(component, out var node))
            {
                if (node.TypeInfo.Constructor != null)
                {
                    foreach (var param in node.TypeInfo.Constructor.Parameters)
                    {
                        if (_serviceProviders.TryGetValue(param.Type, out var provider))
                        {
                            if (componentSet.Contains(provider.Symbol))
                            {
                                graph[component].Add(provider.Symbol);
                            }
                        }
                    }
                }
            }
        }

        // 从第一个有依赖的节点开始构建路径
        var orderedPath = new List<ITypeSymbol>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // 找到起始节点（优先选择有依赖的节点）
        var start = components.FirstOrDefault(c => graph[c].Count > 0) ?? components[0];

        BuildOrderedPath(start, graph, componentSet, visited, orderedPath);

        return orderedPath.Count > 0 ? orderedPath : components;
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
}
