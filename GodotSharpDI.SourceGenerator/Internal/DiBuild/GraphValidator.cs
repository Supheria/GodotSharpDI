using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 依赖图验证器
/// 职责：验证依赖关系的正确性（循环依赖、缺失服务等）
/// </summary>
internal static class GraphValidator
{
    /// <summary>
    /// 验证 Host 服务引用
    /// </summary>
    public static void ValidateHostServices(
        ImmutableArray<ValidatedTypeInfo> hosts,
        ServiceProviderMap serviceProviders,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        // 当前实现为空，预留给未来的 Host 服务验证逻辑
        // 可以在这里添加对 Host 提供的服务的额外验证
    }

    /// <summary>
    /// 验证依赖图
    /// </summary>
    public static void ValidateDependencyGraph(
        ImmutableArray<TypeNode> serviceNodes,
        ImmutableArray<TypeNode> allUserNodes,
        ServiceProviderMap serviceProviders,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            // 1. 检查循环依赖
            ValidateCircularDependencies(serviceNodes, serviceProviders, diagnostics);

            // 2. 检查 User 注入成员
            ValidateUserInjections(allUserNodes, serviceProviders, diagnostics);
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

    // ============================================================
    // 私有验证方法
    // ============================================================

    /// <summary>
    /// 验证循环依赖
    /// </summary>
    private static void ValidateCircularDependencies(
        ImmutableArray<TypeNode> serviceNodes,
        ServiceProviderMap serviceProviders,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            var serviceImplToNode = BuildServiceNodeMap(serviceNodes);

            var detector = new CircularDependencyDetector(serviceImplToNode, serviceProviders);
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

    // TODO: 还需要验证 Host 和 Service 的成员注入

    /// <summary>
    /// 验证 User 注入成员
    /// </summary>
    private static void ValidateUserInjections(
        ImmutableArray<TypeNode> allUserNodes,
        ServiceProviderMap serviceProviders,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var node in allUserNodes)
        {
            try
            {
                ValidateUserInjection(node, serviceProviders, diagnostics);
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
    /// 验证单个 User 的注入
    /// </summary>
    private static void ValidateUserInjection(
        TypeNode node,
        ServiceProviderMap serviceProviders,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var dep in node.Dependencies)
        {
            if (dep.Source == DependencySource.InjectMember)
            {
                if (!serviceProviders.ContainsKey(dep.TargetType))
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

    // ============================================================
    // 辅助方法
    // ============================================================

    private static Dictionary<ITypeSymbol, TypeNode> BuildServiceNodeMap(
        ImmutableArray<TypeNode> serviceNodes
    )
    {
        var map = new Dictionary<ITypeSymbol, TypeNode>(SymbolEqualityComparer.Default);

        foreach (var node in serviceNodes)
        {
            map[node.ValidatedTypeInfo.Symbol] = node;
        }

        return map;
    }
}
