using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 服务提供者映射表类型
/// Key: 暴露的服务类型, Value: 提供该服务的类型信息
/// </summary>
internal sealed class ServiceProviderMap : Dictionary<ITypeSymbol, ValidatedTypeInfo>
{
    public ServiceProviderMap()
        : base(SymbolEqualityComparer.Default) { }
}

/// <summary>
/// 服务提供者映射构建器
/// 职责：构建从服务类型到提供者的映射，检测冲突
/// </summary>
internal static class ServiceProviderMapBuilder
{
    /// <summary>
    /// 构建服务提供者映射
    /// </summary>
    public static ServiceProviderMap Build(
        ImmutableArray<ValidatedTypeInfo> hosts,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        try
        {
            var map = new ServiceProviderMap();
            var conflictTracker = new ConflictTracker();

            // 注册 Host 提供的服务
            RegisterServicesFromHosts(hosts, map, conflictTracker, diagnostics);

            // 报告所有冲突
            ReportConflicts(conflictTracker, diagnostics);

            return map;
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.GraphBuildPhaseFailed,
                    "BuildServiceProviderMap",
                    ex.Message
                )
            );
            return new ServiceProviderMap();
        }
    }

    /// <summary>
    /// 从 Host 类型注册服务
    /// </summary>
    private static void RegisterServicesFromHosts(
        IEnumerable<ValidatedTypeInfo> hosts,
        ServiceProviderMap map,
        ConflictTracker conflictTracker,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var host in hosts)
        {
            try
            {
                foreach (var member in host.Members)
                {
                    if (member.IsProvideMember)
                    {
                        foreach (var exposedType in member.ExposedTypes)
                        {
                            var providerDesc =
                                $"{host.Symbol.ToDisplayString()}.{member.Symbol.Name}";
                            AddProvider(exposedType, host, providerDesc, map, conflictTracker);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ServiceProviderRegistrationFailed,
                        host.Location,
                        host.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }
    }

    /// <summary>
    /// 添加服务提供者，检测冲突
    /// </summary>
    private static void AddProvider(
        ITypeSymbol exposedType,
        ValidatedTypeInfo provider,
        string providerDescription,
        ServiceProviderMap map,
        ConflictTracker conflictTracker
    )
    {
        try
        {
            if (!map.TryGetValue(exposedType, out var existing))
            {
                map[exposedType] = provider;
                return;
            }

            // 发现冲突
            conflictTracker.AddConflict(
                exposedType,
                existing.Symbol.ToDisplayString(),
                providerDescription
            );
        }
        catch (Exception ex)
        {
            // 这个异常会被上层捕获
            throw new InvalidOperationException(
                $"Failed to add provider for {exposedType?.ToDisplayString() ?? "<unknown>"}",
                ex
            );
        }
    }

    /// <summary>
    /// 报告所有冲突
    /// </summary>
    private static void ReportConflicts(
        ConflictTracker conflictTracker,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var (exposedType, providers) in conflictTracker.GetConflicts())
        {
            var providersText = string.Join(", ", providers);
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.ServiceTypeConflict,
                    exposedType.ToDisplayString(),
                    providersText
                )
            );
        }
    }

    // ============================================================
    // 冲突跟踪器 - 辅助类
    // ============================================================

    private sealed class ConflictTracker
    {
        private readonly Dictionary<ITypeSymbol, List<string>> _conflicts = new(
            SymbolEqualityComparer.Default
        );

        public void AddConflict(
            ITypeSymbol exposedType,
            string firstProvider,
            string secondProvider
        )
        {
            if (!_conflicts.TryGetValue(exposedType, out var providers))
            {
                providers = new List<string> { firstProvider };
                _conflicts[exposedType] = providers;
            }
            providers.Add(secondProvider);
        }

        public IEnumerable<(ITypeSymbol ExposedType, List<string> Providers)> GetConflicts()
        {
            foreach (var kvp in _conflicts)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }
    }
}
