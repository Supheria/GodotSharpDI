using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 跨 Host WaitFor 死锁检测器（编译期）
/// 在服务类型层面构建全局依赖图并运行 Tarjan SCC 算法
/// 边语义：S → T 表示"提供服务 S 的 Host 的 WaitFor 需要等待 T 注入完成"
/// </summary>
internal sealed class CrossHostCircularDependencyDetector
{
    private readonly ImmutableDictionary<ITypeSymbol, ImmutableArray<ITypeSymbol>> _graph;
    private readonly ServiceIndexes _indexes;

    private Dictionary<ITypeSymbol, int> _disc  = new(SymbolEqualityComparer.Default);
    private Dictionary<ITypeSymbol, int> _low   = new(SymbolEqualityComparer.Default);
    private HashSet<ITypeSymbol>         _onStack = new(SymbolEqualityComparer.Default);
    private Stack<ITypeSymbol>            _stack = new();
    private int _timer = 0;
    private List<List<ITypeSymbol>> _cycles = new();

    public CrossHostCircularDependencyDetector(
        ImmutableDictionary<ITypeSymbol, ImmutableArray<ITypeSymbol>> graph,
        ServiceIndexes indexes)
    {
        _graph   = graph;
        _indexes = indexes;
    }

    public ImmutableArray<Diagnostic> Detect()
    {
        _disc  = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        _low   = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        _onStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        _stack = new Stack<ITypeSymbol>();
        _timer = 0;
        _cycles = new List<List<ITypeSymbol>>();

        foreach (var node in _graph.Keys)
            if (!_disc.ContainsKey(node)) Tarjan(node);

        return BuildDiagnostics();
    }

    private void Tarjan(ITypeSymbol v)
    {
        _disc[v] = _low[v] = _timer++;
        _stack.Push(v);
        _onStack.Add(v);

        if (_graph.TryGetValue(v, out var neighbors))
        {
            foreach (var w in neighbors)
            {
                if (!_disc.ContainsKey(w))
                {
                    Tarjan(w);
                    _low[v] = Math.Min(_low[v], _low[w]);
                }
                else if (_onStack.Contains(w))
                    _low[v] = Math.Min(_low[v], _disc[w]);
            }
        }

        if (_low[v] != _disc[v]) return; // 非 SCC 根，继续

        var scc = new List<ITypeSymbol>();
        ITypeSymbol w2;
        do
        {
            w2 = _stack.Pop();
            _onStack.Remove(w2);
            scc.Add(w2);
        }
        while (!SymbolEqualityComparer.Default.Equals(w2, v));

        if (scc.Count <= 1) return; // 单节点无循环

        // 检查是否所有服务都由「同一个」Host 提供
        // 若是，则属于同 Host 内 WaitFor 环（GDI_D010 已处理），不应报 GDI_D011
        var distinctHosts = scc
            .SelectMany(s => _indexes.FindProviders(s))
            .Select(n => n.ValidatedTypeInfo.Symbol)
            .Distinct(SymbolEqualityComparer.Default)
            .Count();

        if (distinctHosts > 1)
            _cycles.Add(scc);
    }

    private ImmutableArray<Diagnostic> BuildDiagnostics()
    {
        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var cycle in _cycles)
        {
            // 构建可读路径：IServiceA -> IServiceB -> IServiceA
            var path = string.Join(" -> ", cycle.Select(t => t.Name))
                       + " -> " + cycle[0].Name;

            foreach (var svcType in cycle)
            {
                var providers = _indexes.FindProviders(svcType);
                foreach (var p in providers)
                    diags.Add(DiagnosticBuilder.Create(
                        DiagnosticDescriptors.CrossHostDeadlockDetected,
                        p.ValidatedTypeInfo.Location,
                        path));
            }
        }
        return diags.ToImmutable();
    }
}
