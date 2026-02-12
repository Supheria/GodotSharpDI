using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 依赖图构建器 - 主入口（重构版）
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
            var nodes = BuildAllNodes(typesByRole, symbols, diagnostics);
            GraphValidator.ValidateDependencyGraph(
                nodes.HostNodes,
                nodes.UserNodes,
                nodes.ServiceProviderMap,
                symbols,
                diagnostics
            );

            // 6. 组装最终图
            try
            {
                var graph = new DiGraph(
                    HostNodes: nodes.HostNodes,
                    UserNodes: nodes.UserNodes,
                    ScopeNodes: nodes.ScopeNodes,
                    ServiceProviderMap: nodes.ServiceProviderMap
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
            // 顶层异常捕获
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(DiagnosticDescriptors.GraphBuildFailed, ex.Message)
            );
            return new DiGraphBuildResult(null, diagnostics.ToImmutable());
        }
    }

    /// <summary>
    /// 按角色分类类型
    /// </summary>
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

    /// <summary>
    /// 构建所有类型的节点
    /// </summary>
    private static AllNodes BuildAllNodes(
        TypesByRole types,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var hostNodes = NodeBuilders.BuildHostNodes(types.Hosts, diagnostics);

        var userNodes = NodeBuilders.BuildUserNodes(types.Users, diagnostics);

        // ===== 修复: 正确构建 ServiceProviderMap =====
        // Key 应该是暴露的服务类型（IServiceA, IServiceB），而不是 Host 类型
        var serviceProviderMap = BuildServiceProviderMap(hostNodes, diagnostics);

        // ===== 新增: 构建 Host 类型到 TypeNode 的映射 =====
        // 用于 Scope 验证，确保每个 Host 都能被找到
        var hostTypeToNode = BuildHostTypeToNodeMap(hostNodes);

        var scopeNodes = NodeBuilders.BuildScopeNodes(
            types.Scopes,
            symbols,
            diagnostics,
            serviceProviderMap,
            hostTypeToNode // 传递新的映射
        );

        return new AllNodes(
            HostNodes: hostNodes,
            UserNodes: userNodes,
            ScopeNodes: scopeNodes,
            ServiceProviderMap: serviceProviderMap
        );
    }

    /// <summary>
    /// 构建服务提供者映射表
    /// Key: 暴露的服务类型（如 IServiceA, IServiceB）
    /// Value: 提供该服务的 TypeNode
    /// </summary>
    private static ServiceProviderMap BuildServiceProviderMap(
        ImmutableArray<TypeNode> hostNodes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var serviceProviderMap = new ServiceProviderMap();

        foreach (var node in hostNodes)
        {
            // 遍历该 Host 提供的所有服务类型
            foreach (var providedService in node.ProvidedServices)
            {
                // 检查是否有重复提供
                if (serviceProviderMap.ContainsKey(providedService))
                {
                    // 全局层面允许多个Host提供同一服务（可能用于不同Scope）
                    // 这里只使用第一个提供者，不报告错误
                    // Scope级别的冲突会在ValidateScopeServiceConflicts中检测
                    continue;
                }

                // 将服务类型映射到提供它的 Host 节点
                serviceProviderMap[providedService] = node;
            }
        }

        return serviceProviderMap;
    }

    /// <summary>
    /// 构建 Host 类型到 TypeNode 的映射
    /// 用于 Scope 验证中查找每个 Host 的完整信息
    /// </summary>
    private static ImmutableDictionary<ITypeSymbol, TypeNode> BuildHostTypeToNodeMap(
        ImmutableArray<TypeNode> hostNodes
    )
    {
        var builder = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );

        foreach (var node in hostNodes)
        {
            builder[node.ValidatedTypeInfo.Symbol] = node;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// 构建 Host 节点映射字典
    /// </summary>
    private static ImmutableDictionary<ITypeSymbol, TypeNode> BuildHostNodeMap(
        AllNodes nodes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            var builder = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
                SymbolEqualityComparer.Default
            );

            foreach (var node in nodes.HostNodes)
            {
                builder[node.ValidatedTypeInfo.Symbol] = node;
            }

            return builder.ToImmutable();
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.GraphBuildPhaseFailed,
                    "BuildHostNodeMap",
                    ex.Message
                )
            );
            return ImmutableDictionary<ITypeSymbol, TypeNode>.Empty;
        }
    }

    // ============================================================
    // 内部数据结构 - 用于组织构建过程
    // ============================================================

    private record TypesByRole(
        ImmutableArray<ValidatedTypeInfo> Hosts,
        ImmutableArray<ValidatedTypeInfo> Users,
        ImmutableArray<ValidatedTypeInfo> Scopes
    );

    private record AllNodes(
        ImmutableArray<TypeNode> HostNodes,
        ImmutableArray<TypeNode> UserNodes,
        ImmutableArray<ScopeNode> ScopeNodes,
        ServiceProviderMap ServiceProviderMap
    );
}
