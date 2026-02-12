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
/// </summary>
internal sealed class CircularDependencyDetector
{
    private readonly Dictionary<ITypeSymbol, TypeNode> _serviceImplToNode;
    private readonly Dictionary<ITypeSymbol, ValidatedTypeInfo> _serviceProviders;

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
        Dictionary<ITypeSymbol, ValidatedTypeInfo> serviceProviders
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
            if (!_indices.ContainsKey(node.ValidatedTypeInfo.Symbol))
            {
                StrongConnect(node.ValidatedTypeInfo.Symbol);
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
        _indices[typeSymbol] = _index;
        _lowLinks[typeSymbol] = _index;
        _index++;
        _stack.Push(typeSymbol);
        _onStack.Add(typeSymbol);

        // 获取该类型的节点
        if (!_serviceImplToNode.TryGetValue(typeSymbol, out var node))
            return;

        // 遍历所有依赖
        foreach (var dependency in node.Dependencies)
        {
            // 解析依赖的实际提供者
            if (!_serviceProviders.TryGetValue(dependency.TargetType, out var provider))
                continue;

            var dependencyImpl = provider.Symbol;

            // 只检查服务间的依赖（不检查 User 的 Inject）
            if (!_serviceImplToNode.ContainsKey(dependencyImpl))
                continue;

            if (!_indices.ContainsKey(dependencyImpl))
            {
                // 递归访问未访问的依赖
                StrongConnect(dependencyImpl);
                _lowLinks[typeSymbol] = Math.Min(_lowLinks[typeSymbol], _lowLinks[dependencyImpl]);
            }
            else if (_onStack.Contains(dependencyImpl))
            {
                // 发现后向边（循环依赖）
                _lowLinks[typeSymbol] = Math.Min(_lowLinks[typeSymbol], _indices[dependencyImpl]);
            }
        }

        // 检查是否是强连通分量的根
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

            // 如果强连通分量包含多个节点，或有自环，则是循环依赖
            if (component.Count > 1 || HasSelfLoop(typeSymbol))
            {
                _cycles.Add(new Cycle(component));
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

        // 检查是否有指向自己的依赖边
        foreach (var dependency in node.Dependencies)
        {
            if (_serviceProviders.TryGetValue(dependency.TargetType, out var provider))
            {
                if (SymbolEqualityComparer.Default.Equals(provider.Symbol, typeSymbol))
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
            // 构建循环路径
            var cyclePath = BuildCyclePath(cycle.Components);

            // ===== 修复: 为循环中的每个节点都生成诊断 =====
            // 这样用户可以在所有涉及循环依赖的类中看到错误提示
            foreach (var component in cycle.Components)
            {
                if (_serviceImplToNode.TryGetValue(component, out var node))
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
        if (components.Count <= 1)
            return components;

        // 构建循环内的依赖图
        var graph = new Dictionary<ITypeSymbol, List<ITypeSymbol>>(SymbolEqualityComparer.Default);
        var componentSet = new HashSet<ITypeSymbol>(components, SymbolEqualityComparer.Default);

        foreach (var component in components)
        {
            graph[component] = new List<ITypeSymbol>();

            if (_serviceImplToNode.TryGetValue(component, out var node))
            {
                foreach (var dependency in node.Dependencies)
                {
                    if (
                        _serviceProviders.TryGetValue(dependency.TargetType, out var provider)
                        && componentSet.Contains(provider.Symbol)
                    )
                    {
                        graph[component].Add(provider.Symbol);
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
}
