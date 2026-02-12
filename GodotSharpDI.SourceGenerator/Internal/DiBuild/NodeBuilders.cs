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
        ServiceIndexes indexes
    )
    {
        var nodes = ImmutableArray.CreateBuilder<ScopeNode>();

        foreach (var scope in scopes)
        {
            try
            {
                var node = BuildScopeNode(scope, symbols, diagnostics, indexes);
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
                // 获取该Provide成员提供的第一个服务类型（用于标识这个成员）
                var providedType = member.ExposedTypes.FirstOrDefault();

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
                                Source: DependencySource.WaitForMember,
                                SourceMemberName: member.Symbol.Name, // 记录源成员名称
                                SourceProvidedType: providedType // 记录源成员提供的服务类型
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
        ServiceIndexes indexes
    )
    {
        if (scope.ModulesInfo == null)
            return null;

        var hosts = scope.ModulesInfo.Hosts;

        // 验证 Hosts
        ValidateScopeHosts(scope, hosts, symbols, diagnostics);

        // 验证 Scope 内的服务类型冲突
        ValidateScopeServiceConflicts(scope, hosts, indexes, diagnostics);

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
    /// 重构版：直接使用索引，逻辑更清晰
    /// </summary>
    private static void ValidateScopeServiceConflicts(
        ValidatedTypeInfo scope,
        ImmutableArray<INamedTypeSymbol> hosts,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        // 收集该Scope内所有Host提供的服务
        var scopeServices = new Dictionary<ITypeSymbol, List<ServiceProvider>>(
            SymbolEqualityComparer.Default
        );

        foreach (var hostType in hosts)
        {
            // 查找Host节点
            if (!indexes.HostTypeToNode.TryGetValue(hostType, out var hostNode))
                continue;

            // 收集该Host提供的所有服务
            foreach (var providedService in hostNode.ProvidedServices)
            {
                if (!scopeServices.ContainsKey(providedService))
                {
                    scopeServices[providedService] = new List<ServiceProvider>();
                }

                var memberName = FindProviderMemberName(hostNode, providedService);
                scopeServices[providedService]
                    .Add(new ServiceProvider(hostType, hostNode, memberName));
            }
        }

        // 检测冲突：如果一个服务有多个提供者，报告错误
        foreach (var kvp in scopeServices)
        {
            var serviceType = kvp.Key;
            var providers = kvp.Value;

            if (providers.Count > 1)
            {
                // 构建提供者列表字符串
                var providerDescriptions = providers
                    .Select(p => $"{p.HostType.ToDisplayString()}.{p.MemberName}")
                    .ToList();

                var providersText = string.Join(", ", providerDescriptions);

                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeServiceTypeConflict,
                        scope.Location,
                        scope.Symbol.Name,
                        serviceType.ToDisplayString(),
                        providersText
                    )
                );
            }
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

    /// <summary>
    /// 服务提供者信息
    /// </summary>
    private record ServiceProvider(ITypeSymbol HostType, TypeNode HostNode, string MemberName);
}
