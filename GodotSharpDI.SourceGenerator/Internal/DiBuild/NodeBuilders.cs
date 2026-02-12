using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 节点构建器集合
/// 职责：为各种角色类型构建对应的节点
/// </summary>
internal static class NodeBuilders
{
    /// <summary>
    /// 构建 Host 节点
    /// </summary>
    public static ImmutableArray<TypeNode> BuildHostNodes(
        ImmutableArray<ValidatedTypeInfo> hosts,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();

        foreach (var host in hosts)
        {
            try
            {
                var node = BuildHostNode(host);
                nodes.Add(node);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        host.Location,
                        "Host",
                        host.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

    /// <summary>
    /// 构建 User 节点
    /// </summary>
    public static ImmutableArray<TypeNode> BuildUserNodes(
        ImmutableArray<ValidatedTypeInfo> users,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();

        foreach (var user in users)
        {
            try
            {
                var node = BuildUserNode(user);
                nodes.Add(node);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        user.Location,
                        "User",
                        user.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

    /// <summary>
    /// 构建 Scope 节点
    /// </summary>
    public static ImmutableArray<ScopeNode> BuildScopeNodes(
        ImmutableArray<ValidatedTypeInfo> scopes,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ServiceProviderMap serviceProviderMap
    )
    {
        var nodes = ImmutableArray.CreateBuilder<ScopeNode>();

        foreach (var scope in scopes)
        {
            try
            {
                var node = BuildScopeNode(scope, symbols, diagnostics, serviceProviderMap);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        scope.Location,
                        "Scope",
                        scope.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

    // ============================================================
    // 私有构建方法 - 每个角色一个
    // ============================================================

    private static TypeNode BuildServiceNode(ValidatedTypeInfo service, CachedSymbols symbols)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();
        var providedServices = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // 收集 Inject 成员依赖
        foreach (var member in service.Members)
        {
            if (member.IsInjectMember)
            {
                dependencies.Add(
                    new DependencyEdge(
                        TargetType: member.MemberType,
                        Location: member.Location,
                        Source: DependencySource.InjectMember
                    )
                );
            }
        }

        // 收集 Provide 成员的 WaitFor 依赖
        foreach (var member in service.Members)
        {
            if (member.IsProvideMember && member.HasWaitFor)
            {
                foreach (var waitForFieldName in member.WaitFor)
                {
                    // 查找 WaitFor 引用的字段
                    var waitForField = service.Members.FirstOrDefault(m =>
                        m.Symbol.Name == waitForFieldName
                    );

                    if (waitForField != null && waitForField.IsInjectMember)
                    {
                        dependencies.Add(
                            new DependencyEdge(
                                TargetType: waitForField.MemberType,
                                Location: member.Location,
                                Source: DependencySource.WaitForMember
                            )
                        );
                    }
                }
            }
        }

        // 收集 Provides 成员提供的服务
        foreach (var member in service.Members)
        {
            if (member.IsProvideMember)
            {
                providedServices.AddRange(member.ExposedTypes);
            }
        }

        return new TypeNode(
            ValidatedTypeInfo: service,
            Dependencies: dependencies.ToImmutable(),
            ProvidedServices: providedServices.ToImmutable()
        );
    }

    private static TypeNode BuildHostNode(ValidatedTypeInfo host)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();
        var providedServices = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // 收集 Inject 成员依赖
        foreach (var member in host.Members)
        {
            if (member.IsInjectMember)
            {
                dependencies.Add(
                    new DependencyEdge(
                        TargetType: member.MemberType,
                        Location: member.Location,
                        Source: DependencySource.InjectMember
                    )
                );
            }
        }

        // 收集 Provide 成员的 WaitFor 依赖
        foreach (var member in host.Members)
        {
            if (member.IsProvideMember && member.HasWaitFor)
            {
                foreach (var waitForFieldName in member.WaitFor)
                {
                    // 查找 WaitFor 引用的字段
                    var waitForField = host.Members.FirstOrDefault(m =>
                        m.Symbol.Name == waitForFieldName
                    );

                    if (waitForField != null && waitForField.IsInjectMember)
                    {
                        dependencies.Add(
                            new DependencyEdge(
                                TargetType: waitForField.MemberType,
                                Location: member.Location,
                                Source: DependencySource.WaitForMember
                            )
                        );
                    }
                }
            }
        }

        // 收集 Provides 成员提供的服务
        foreach (var member in host.Members)
        {
            if (member.IsProvideMember)
            {
                providedServices.AddRange(member.ExposedTypes);
            }
        }

        return new TypeNode(
            ValidatedTypeInfo: host,
            Dependencies: dependencies.ToImmutable(),
            ProvidedServices: providedServices.ToImmutable()
        );
    }

    private static TypeNode BuildUserNode(ValidatedTypeInfo user)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();

        // 收集注入依赖
        foreach (var member in user.Members)
        {
            if (member.IsInjectMember)
            {
                dependencies.Add(
                    new DependencyEdge(
                        TargetType: member.MemberType,
                        Location: member.Location,
                        Source: DependencySource.InjectMember
                    )
                );
            }
        }

        return new TypeNode(
            ValidatedTypeInfo: user,
            Dependencies: dependencies.ToImmutable(),
            ProvidedServices: ImmutableArray<INamedTypeSymbol>.Empty
        );
    }

    private static ScopeNode? BuildScopeNode(
        ValidatedTypeInfo scope,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ServiceProviderMap serviceProviderMap
    )
    {
        if (scope.ModulesInfo == null)
            return null;
        var hosts = scope.ModulesInfo.Hosts;

        // 验证 Hosts
        ValidateScopeHosts(scope, hosts, symbols, diagnostics);

        // 验证 Scope 内的服务类型冲突
        ValidateScopeServiceConflicts(scope, hosts, serviceProviderMap, diagnostics);

        // 检查是否为空
        if (hosts.IsEmpty)
        {
            diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeModulesEmpty,
                    scope.Location,
                    scope.Symbol.Name
                )
            );
        }

        return new ScopeNode(ValidatedTypeInfo: scope, ExpectHosts: hosts);
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private static void ValidateScopeHosts(
        ValidatedTypeInfo scope,
        ImmutableArray<INamedTypeSymbol> hosts,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var type in hosts)
        {
            var isHost = type.HasAttribute(symbols.HostAttribute);

            if (!isHost)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeModulesHostMustBeHost,
                        scope.Location,
                        scope.Symbol.Name,
                        type.ToDisplayString()
                    )
                );
            }
        }
    }

    /// <summary>
    /// 验证 Scope 内的服务类型冲突
    /// </summary>
    private static void ValidateScopeServiceConflicts(
        ValidatedTypeInfo scope,
        ImmutableArray<INamedTypeSymbol> hosts,
        ServiceProviderMap serviceProviderMap,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var conflictTracker = new ServiceConflictTracker();

        // 跟踪每个服务类型第一次出现的提供者
        var serviceToFirstProvider = new Dictionary<ITypeSymbol, (ITypeSymbol Host, string Member)>(
            SymbolEqualityComparer.Default
        );

        // 遍历 Scope 中的所有 Host
        foreach (var hostType in hosts)
        {
            if (!serviceProviderMap.TryGetValue(hostType, out var hostNode))
                continue;

            // 检查每个 Host 提供的服务
            foreach (var exposedType in hostNode.ProvidedServices)
            {
                var memberName = FindProviderMemberName(hostNode, exposedType);
                var currentProviderDesc = $"{hostType.ToDisplayString()}.{memberName}";

                if (!serviceToFirstProvider.TryGetValue(exposedType, out var firstProvider))
                {
                    // 第一次遇到这个服务类型，记录下来
                    serviceToFirstProvider[exposedType] = (hostType, memberName);
                }
                else
                {
                    // 检测到冲突！这个服务类型之前已经被另一个 Host 提供过
                    var firstProviderDesc =
                        $"{firstProvider.Host.ToDisplayString()}.{firstProvider.Member}";
                    conflictTracker.AddConflict(
                        exposedType,
                        firstProviderDesc,
                        currentProviderDesc
                    );
                }
            }
        }

        // 报告 Scope 级别的所有冲突
        foreach (var (exposedType, providers) in conflictTracker.GetConflicts())
        {
            var providersText = string.Join(", ", providers);
            diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeServiceTypeConflict, // GDI_D011
                    scope.Location,
                    scope.Symbol.Name,
                    exposedType.ToDisplayString(),
                    providersText
                )
            );
        }
    }

    /// <summary>
    /// 查找提供特定服务类型的成员名称
    /// </summary>
    private static string FindProviderMemberName(TypeNode hostNode, ITypeSymbol exposedType)
    {
        foreach (var member in hostNode.ValidatedTypeInfo.Members)
        {
            if (member.IsProvideMember)
            {
                foreach (var exposed in member.ExposedTypes)
                {
                    if (SymbolEqualityComparer.Default.Equals(exposed, exposedType))
                    {
                        return member.Symbol.Name;
                    }
                }
            }
        }

        return "<unknown>";
    }

    // ============================================================
    // 冲突跟踪器 - 辅助类
    // ============================================================

    private sealed class ServiceConflictTracker
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
