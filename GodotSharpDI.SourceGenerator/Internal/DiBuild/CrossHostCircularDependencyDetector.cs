using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Cross-Host WaitFor deadlock detector (compile-time)
/// Builds a global dependency graph at the service type level and runs Tarjan SCC algorithm
/// Edge semantics: S → T means "Host providing service S needs to wait for T injection to complete in WaitFor"
/// </summary>
internal sealed class CrossHostCircularDependencyDetector
{
    private readonly ImmutableDictionary<ITypeSymbol, ImmutableArray<ITypeSymbol>> _graph;
    private readonly ServiceIndexes _indexes;

    public CrossHostCircularDependencyDetector(
        ImmutableDictionary<ITypeSymbol, ImmutableArray<ITypeSymbol>> graph,
        ServiceIndexes indexes)
    {
        _graph   = graph;
        _indexes = indexes;
    }

    public ImmutableArray<Diagnostic> Detect()
    {
        // Convert to IReadOnlyDictionary for TarjanSCC
        // Note: SymbolEqualityComparer implements IEqualityComparer<ISymbol>,
        // which works for ITypeSymbol keys via covariance in Detect().
        var graphForTarjan = new Dictionary<ITypeSymbol, IEnumerable<ITypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var kvp in _graph)
            graphForTarjan[kvp.Key] = kvp.Value;

        var allSccs = TarjanSCC<ITypeSymbol>.Detect(graphForTarjan, SymbolEqualityComparer.Default);

        // Filter: keep multi-node SCCs where services are provided by different Hosts
        var crossHostCycles = FilterCrossHostCycles(allSccs);

        return BuildDiagnostics(crossHostCycles);
    }

    /// <summary>
    /// Filter SCCs: keep multi-node SCCs where services span multiple Hosts.
    /// Single-node SCCs and same-Host SCCs are excluded (same-Host cycles are handled by GDI_D010).
    /// </summary>
    private List<List<ITypeSymbol>> FilterCrossHostCycles(List<List<ITypeSymbol>> sccs)
    {
        var result = new List<List<ITypeSymbol>>();

        foreach (var scc in sccs)
        {
            if (scc.Count <= 1)
                continue;

            var distinctHosts = scc
                .SelectMany(s => _indexes.FindProviders(s))
                .Select(n => n.ValidatedTypeInfo.Symbol)
                .Distinct(SymbolEqualityComparer.Default)
                .Count();

            if (distinctHosts > 1)
                result.Add(scc);
        }

        return result;
    }

    private ImmutableArray<Diagnostic> BuildDiagnostics(List<List<ITypeSymbol>> cycles)
    {
        var diags = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var cycle in cycles)
        {
            // Build readable path: IServiceA -> IServiceB -> IServiceA
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
