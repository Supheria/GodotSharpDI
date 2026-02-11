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
        // TODO: 需要重写（已经从构造函数的依赖模式转变为 WaitFor 模式）
    }

    /// <summary>
    /// 检查节点是否有自环（依赖自己）
    /// </summary>
    private bool HasSelfLoop(ITypeSymbol typeSymbol)
    {
        // TODO: 需要重写（已经从构造函数的依赖模式转变为 WaitFor 模式）
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
                    firstNode.ValidatedTypeInfo.Location,
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
        // TODO: 需要重写（已经从构造函数的依赖模式转变为 WaitFor 模式）
        return [];
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
