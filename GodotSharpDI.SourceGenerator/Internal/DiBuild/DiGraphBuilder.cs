using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 依赖图构建器 - 重构版
/// 职责：协调各个构建器，组装最终的依赖图
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
            // 1. 提取有效类型
            var validTypes = classResults
                .Where(r => r.TypeInfo != null)
                .Select(r => r.TypeInfo!)
                .ToImmutableArray();

            if (validTypes.IsEmpty)
                return DiGraphBuildResult.Empty;

            // 2. 按角色分类
            var typesByRole = ClassifyTypesByRole(validTypes, diagnostics);
            if (typesByRole == null)
                return new DiGraphBuildResult(null, diagnostics.ToImmutable());

            // 3. 构建各类节点
            var nodes = BuildAllNodes(typesByRole, diagnostics);

            // 4. 构建全局索引
            var indexes = ServiceIndexes.Build(nodes.HostNodes, nodes.UserNodes);

            // 5. 验证依赖图
            GraphValidator.ValidateDependencyGraph(
                nodes.HostNodes,
                nodes.UserNodes,
                indexes,
                symbols,
                diagnostics
            );

            // 6. 构建并验证Scope节点
            var scopeNodes = NodeBuilders.BuildScopeNodes(
                typesByRole.Scopes,
                symbols,
                diagnostics,
                indexes
            );

            // 7. 组装最终图
            try
            {
                var graph = new DiGraph(
                    HostNodes: nodes.HostNodes,
                    UserNodes: nodes.UserNodes,
                    ScopeNodes: scopeNodes,
                    ServiceProviderMap: BuildLegacyServiceProviderMap(indexes) // 为了兼容性保留
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

    /// <summary>
    /// 构建传统的ServiceProviderMap（为了向后兼容）
    /// 新代码应该使用ServiceIndexes
    /// </summary>
    private static ServiceProviderMap BuildLegacyServiceProviderMap(ServiceIndexes indexes)
    {
        var map = new ServiceProviderMap();

        // 为每个服务类型选择第一个提供者（保持向后兼容）
        foreach (var kvp in indexes.ServiceTypeToProviders)
        {
            if (kvp.Value.Length > 0)
            {
                map[kvp.Key] = kvp.Value[0];
            }
        }

        return map;
    }

    // ============================================================
    // 内部数据结构
    // ============================================================

    private record TypesByRole(
        ImmutableArray<ValidatedTypeInfo> Hosts,
        ImmutableArray<ValidatedTypeInfo> Users,
        ImmutableArray<ValidatedTypeInfo> Scopes
    );

    private record AllNodes(ImmutableArray<TypeNode> HostNodes, ImmutableArray<TypeNode> UserNodes);
}
