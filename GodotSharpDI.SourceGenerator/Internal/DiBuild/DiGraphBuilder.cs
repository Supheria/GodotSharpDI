using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Dependency graph builder - refactored version
/// Responsibility: Coordinate various builders to assemble the final dependency graph
/// </summary>
internal static class DiGraphBuilder
{
    public static DiGraphBuildResult Build(
        ImmutableArray<ClassValidationResult> classResults,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        try
        {
            // 1. Extract valid types
            var validTypes = classResults
                .Where(r => r.TypeInfo != null)
                .Select(r => r.TypeInfo!)
                .ToImmutableArray();

            if (validTypes.IsEmpty)
                return DiGraphBuildResult.Empty;

            // 2. Classify by role
            var typesByRole = ClassifyTypesByRole(validTypes, diagnostics);
            if (typesByRole == null)
                return new DiGraphBuildResult(null, diagnostics.ToImmutable());

            // 3. Build various nodes
            var nodes = BuildAllNodes(typesByRole, diagnostics);

            // 4. Build global indexes
            var indexes = ServiceIndexes.Build(nodes.HostNodes, nodes.UserNodes);

            // 5. Validate dependency graph
            GraphValidator.ValidateDependencyGraph(
                nodes.HostNodes,
                nodes.UserNodes,
                indexes,
                symbols,
                diagnostics
            );

            // 6. Build and validate Scope nodes
            var scopeNodes = NodeBuilders.BuildScopeNodes(
                typesByRole.Scopes,
                symbols,
                diagnostics,
                indexes
            );

            // 7. Assemble final graph
            try
            {
                var graph = new DiGraph(
                    HostNodes: nodes.HostNodes,
                    UserNodes: nodes.UserNodes,
                    ScopeNodes: scopeNodes,
                    HostNodeMap: indexes.HostTypeToNode
                );

                return new DiGraphBuildResult(graph, diagnostics.ToImmutable());
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.CreateAtNone(
                        DiagnosticDescriptors.GraphBuildPhaseFailed,
                        "CreateDiGraph",
                        ex.Message
                    )
                );
                return new DiGraphBuildResult(null, diagnostics.ToImmutable());
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(DiagnosticDescriptors.GraphBuildFailed, ex.Message)
            );
            return new DiGraphBuildResult(null, diagnostics.ToImmutable());
        }
    }

    private static TypesByRole? ClassifyTypesByRole(
        ImmutableArray<ValidatedTypeInfo> validTypes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            return new TypesByRole(
                Hosts: validTypes.Where(t => t.Role == TypeRole.Host).ToImmutableArray(),
                Users: validTypes.Where(t => t.Role == TypeRole.User).ToImmutableArray(),
                Scopes: validTypes.Where(t => t.Role == TypeRole.Scope).ToImmutableArray()
            );
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.GraphBuildPhaseFailed,
                    "ClassifyByRole",
                    ex.Message
                )
            );
            return null;
        }
    }

    private static AllNodes BuildAllNodes(
        TypesByRole types,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var hostNodes = NodeBuilders.BuildHostNodes(types.Hosts, diagnostics);
        var userNodes = NodeBuilders.BuildUserNodes(types.Users, diagnostics);

        return new AllNodes(HostNodes: hostNodes, UserNodes: userNodes);
    }

    // ============================================================
    // Internal data structures
    // ============================================================

    private record TypesByRole(
        ImmutableArray<ValidatedTypeInfo> Hosts,
        ImmutableArray<ValidatedTypeInfo> Users,
        ImmutableArray<ValidatedTypeInfo> Scopes
    );

    private record AllNodes(ImmutableArray<TypeNode> HostNodes, ImmutableArray<TypeNode> UserNodes);
}
