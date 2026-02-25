using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 依赖图验证器 - 重构版
/// 职责：验证全局依赖关系的正确性（循环依赖、缺失服务等）
/// 不包含Scope级别的验证（Scope验证在NodeBuilders中）
/// </summary>
internal static class GraphValidator
{
    /// <summary>
    /// 验证依赖图
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
            // 1. 检测 Host 节点的循环依赖（包括 WaitFor 循环）
            DetectCircularDependencies(allHostNodes, indexes, diagnostics);

            // 1b. P1: 跨 Host 全局 WaitFor 死锁检测（GDI_D011）
            DetectCrossHostDeadlocks(indexes, diagnostics);

            // 2. 验证 Host 的注入成员
            ValidateHostInjections(allHostNodes, indexes, diagnostics);

            // 3. 验证 User 注入成员
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
    /// 检测循环依赖（包括 WaitFor 形成的循环）
    /// </summary>
    private static void DetectCircularDependencies(
        ImmutableArray<TypeNode> hostNodes,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            // 构建服务类型到提供者的映射（用于循环检测）
            var serviceTypeToProvider = BuildServiceTypeToProviderMap(indexes);

            // 使用 CircularDependencyDetector 检测循环
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
    /// 构建服务类型到提供者的映射（用于循环依赖检测）
    /// 如果有多个提供者，使用第一个（循环检测只需要知道类型间的依赖关系）
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
    /// P1: 跨 Host WaitFor 死锁检测
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
    /// 验证 Host 注入成员
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
    /// 验证 User 注入成员
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
    /// 验证单个节点的注入（Host 和 User 共用）
    /// </summary>
    private static void ValidateNodeInjections(
        TypeNode node,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var dep in node.Dependencies)
        {
            // 只检查 Inject 成员依赖
            // WaitFor 依赖会在循环检测中处理
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
