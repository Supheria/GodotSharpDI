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
    /// 验证依赖图
    /// </summary>
    public static void ValidateDependencyGraph(
        ImmutableArray<TypeNode> allHostNodes,
        ImmutableArray<TypeNode> allUserNodes,
        ServiceProviderMap serviceProviders,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            // 1. 检测 Host 节点的循环依赖（包括 WaitFor 循环）
            DetectCircularDependencies(allHostNodes, diagnostics);

            // 2. 验证 Host 的注入成员
            ValidateHostInjections(allHostNodes, serviceProviders, diagnostics);

            // 3. 验证 User 注入成员
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
    /// 检测循环依赖（包括 WaitFor 形成的循环）
    /// </summary>
    private static void DetectCircularDependencies(
        ImmutableArray<TypeNode> hostNodes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            // 构建服务实现类型到节点的映射
            var serviceImplToNode = new Dictionary<ITypeSymbol, TypeNode>(
                SymbolEqualityComparer.Default
            );

            foreach (var node in hostNodes)
            {
                // Host 节点代表提供服务的类
                serviceImplToNode[node.ValidatedTypeInfo.Symbol] = node;
            }

            // 构建服务类型到提供者的映射（用于循环检测）
            var serviceTypeToProvider = new Dictionary<ITypeSymbol, ValidatedTypeInfo>(
                SymbolEqualityComparer.Default
            );

            foreach (var node in hostNodes)
            {
                // 对于每个 Host 提供的服务类型，记录提供者
                foreach (var providedService in node.ProvidedServices)
                {
                    // 如果多个成员提供同一类型，使用第一个（与 ServiceProviderMap 行为一致）
                    if (!serviceTypeToProvider.ContainsKey(providedService))
                    {
                        serviceTypeToProvider[providedService] = node.ValidatedTypeInfo;
                    }
                }
            }

            // 使用 CircularDependencyDetector 检测循环
            var detector = new CircularDependencyDetector(serviceImplToNode, serviceTypeToProvider);

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
    /// 验证 Host 注入成员
    /// </summary>
    private static void ValidateHostInjections(
        ImmutableArray<TypeNode> hostNodes,
        ServiceProviderMap serviceProviders,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var node in hostNodes)
        {
            try
            {
                ValidateNodeInjections(node, serviceProviders, diagnostics);
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
                ValidateNodeInjections(node, serviceProviders, diagnostics);
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
    /// 验证单个节点的注入（Host 和 User 共用）
    /// </summary>
    private static void ValidateNodeInjections(
        TypeNode node,
        ServiceProviderMap serviceProviders,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var dep in node.Dependencies)
        {
            // 只检查 Inject 成员依赖
            // WaitFor 依赖会在循环检测中处理
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
}
