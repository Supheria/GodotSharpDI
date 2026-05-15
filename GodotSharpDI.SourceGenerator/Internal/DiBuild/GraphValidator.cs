using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Dependency graph validator - refactored version
/// Responsibility: Validate the correctness of global dependencies (circular dependencies, missing services, etc.)
/// Does not include Scope-level validation (Scope validation is in NodeBuilders)
/// </summary>
internal static class GraphValidator
{
    /// <summary>
    /// Validate dependency graph
    /// </summary>
    public static void ValidateDependencyGraph(
        ImmutableArray<TypeNode> allHostNodes,
        ImmutableArray<TypeNode> allUserNodes,
        ServiceIndexes indexes,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            // 1. Detect circular dependencies in Host nodes (including WaitFor cycles)
            DetectCircularDependencies(allHostNodes, indexes, diagnostics);

            // 1b. P1: Cross-Host global WaitFor deadlock detection (GDI_D011)
            DetectCrossHostDeadlocks(indexes, diagnostics);

            // 2. Validate Host injection members
            ValidateHostInjections(allHostNodes, indexes, diagnostics);

            // 3. Validate User injection members
            ValidateUserInjections(allUserNodes, indexes, diagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.GraphValidationFailed,
                    "OverallValidation",
                    ex.Message
                )
            );
        }
    }

    /// <summary>
    /// Detect circular dependencies (including cycles formed by WaitFor)
    /// </summary>
    private static void DetectCircularDependencies(
        ImmutableArray<TypeNode> hostNodes,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            // Build service type to provider mapping (for circular dependency detection)
            var serviceTypeToProvider = BuildServiceTypeToProviderMap(indexes);

            // Use CircularDependencyDetector to detect cycles
            var detector = new CircularDependencyDetector(
                indexes.HostTypeToNode.ToImmutableDictionary(SymbolEqualityComparer.Default),
                serviceTypeToProvider
            );

            var circularDiagnostics = detector.DetectCircularDependencies();
            diagnostics.AddRange(circularDiagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.GraphValidationFailed,
                    "CircularDependencyDetection",
                    ex.Message
                )
            );
        }
    }

    /// <summary>
    /// Build service type to provider mapping (for circular dependency detection)
    /// If there are multiple providers, use the first one (cycle detection only needs to know dependencies between types)
    /// </summary>
    private static ImmutableDictionary<
        ITypeSymbol,
        ValidatedTypeInfo
    > BuildServiceTypeToProviderMap(ServiceIndexes indexes)
    {
        var builder = ImmutableDictionary.CreateBuilder<ITypeSymbol, ValidatedTypeInfo>(
            SymbolEqualityComparer.Default
        );

        foreach (var kvp in indexes.ServiceTypeToProviders)
        {
            if (kvp.Value.Length > 0)
            {
                builder[kvp.Key] = kvp.Value[0].ValidatedTypeInfo;
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// P1: Cross-Host WaitFor deadlock detection
    /// </summary>
    private static void DetectCrossHostDeadlocks(
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            var graphBuilder = ImmutableDictionary.CreateBuilder<
                ITypeSymbol,
                ImmutableArray<ITypeSymbol>
            >(SymbolEqualityComparer.Default);

            foreach (var kvp in indexes.ServiceTypeToWaitForDeps)
            {
                var deps = kvp.Value.Where(d => indexes.HasProvider(d)).ToImmutableArray();

                if (!deps.IsEmpty)
                    graphBuilder[kvp.Key] = deps;
            }

            var detector = new CrossHostCircularDependencyDetector(
                graphBuilder.ToImmutable(),
                indexes
            );
            diagnostics.AddRange(detector.Detect());
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.GraphValidationFailed,
                    "CrossHostDeadlockDetection",
                    ex.Message
                )
            );
        }
    }

    /// <summary>
    /// Validate Host injection members
    /// </summary>
    private static void ValidateHostInjections(
        ImmutableArray<TypeNode> hostNodes,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var node in hostNodes)
        {
            try
            {
                ValidateNodeInjections(node, indexes, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.CreateForSymbol(
                        DiagnosticDescriptors.GraphValidationFailed,
                        node.ValidatedTypeInfo.Symbol,
                        "HostDependencyValidation",
                        ex.Message
                    )
                );
            }
        }
    }

    /// <summary>
    /// Validate User injection members
    /// </summary>
    private static void ValidateUserInjections(
        ImmutableArray<TypeNode> allUserNodes,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var node in allUserNodes)
        {
            try
            {
                ValidateNodeInjections(node, indexes, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.CreateForSymbol(
                        DiagnosticDescriptors.GraphValidationFailed,
                        node.ValidatedTypeInfo.Symbol,
                        "UserDependencyValidation",
                        ex.Message
                    )
                );
            }
        }
    }

    /// <summary>
    /// Validate injection for a single node (shared by Host and User)
    /// </summary>
    private static void ValidateNodeInjections(
        TypeNode node,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var dep in node.Dependencies)
        {
            // Only check Inject member dependencies
            // WaitFor dependencies will be handled in cycle detection
            if (dep.Source == DependencySource.InjectMember)
            {
                if (!indexes.HasProvider(dep.TargetType))
                {
                    diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.InjectMemberTypeIsNotExposed,
                            dep.Location,
                            node.ValidatedTypeInfo.Symbol.Name,
                            dep.TargetType.ToDisplayString()
                        )
                    );
                }
            }
        }
    }
}
