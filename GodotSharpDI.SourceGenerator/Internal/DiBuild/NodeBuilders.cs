using System;
using System.Collections.Immutable;
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
    /// 构建 Service 节点
    /// </summary>
    public static ImmutableArray<TypeNode> BuildServiceNodes(
        ImmutableArray<ValidatedTypeInfo> services,
        ServiceProviderMap serviceProviders,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();

        foreach (var service in services)
        {
            try
            {
                var node = BuildServiceNode(service, symbols);
                nodes.Add(node);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        service.Location,
                        service.Role == TypeRole.Provider ? "Provider" : "Service",
                        service.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

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
        ServiceProviderMap serviceProviders,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var nodes = ImmutableArray.CreateBuilder<ScopeNode>();

        foreach (var scope in scopes)
        {
            try
            {
                var node = BuildScopeNode(scope, symbols, diagnostics);
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

        // 收集构造函数依赖
        if (service.Constructor != null)
        {
            foreach (var param in service.Constructor.Parameters)
            {
                dependencies.Add(
                    new DependencyEdge(
                        TargetType: param.Type,
                        Location: param.Location,
                        Source: DependencySource.Constructor
                    )
                );
            }
        }

        // 收集 Inject 成员依赖（Provider 可能有）
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

        // 获取暴露的服务类型
        var providedServices = GetServiceExposedTypes(service, symbols);

        return new TypeNode(
            ValidatedTypeInfo: service,
            Dependencies: dependencies.ToImmutable(),
            ProvidedServices: providedServices
        );
    }

    private static TypeNode BuildHostNode(ValidatedTypeInfo host)
    {
        var providedServices = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // 收集 Host 提供的服务
        foreach (var member in host.Members)
        {
            if (member.IsSingletonMember || member.IsProvidesMember)
            {
                providedServices.AddRange(member.ExposedTypes);
            }
        }

        return new TypeNode(
            ValidatedTypeInfo: host,
            Dependencies: ImmutableArray<DependencyEdge>.Empty,
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
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        if (scope.ModulesInfo == null)
            return null;

        var services = scope.ModulesInfo.Services;
        var hosts = scope.ModulesInfo.Hosts;

        // 验证 Services
        ValidateScopeServices(scope, services, symbols, diagnostics);

        // 验证 Hosts
        ValidateScopeHosts(scope, hosts, symbols, diagnostics);

        // 检查是否为空
        if (services.IsEmpty && hosts.IsEmpty)
        {
            diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeModulesEmpty,
                    scope.Location,
                    scope.Symbol.Name
                )
            );
        }

        return new ScopeNode(
            ValidatedTypeInfo: scope,
            InstantiateServices: services,
            ExpectHosts: hosts
        );
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>
    /// 获取 Service 或 Provider 暴露的服务类型
    /// </summary>
    private static ImmutableArray<INamedTypeSymbol> GetServiceExposedTypes(
        ValidatedTypeInfo service,
        CachedSymbols symbols
    )
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // 判断是 Provider 还是 Service
        bool isProvider = service.Role == TypeRole.Provider;

        if (isProvider)
        {
            // Provider: 从成员收集暴露的服务类型
            foreach (var member in service.Members)
            {
                if (member.IsProvidesMember || member.IsSingletonMember)
                {
                    builder.AddRange(member.ExposedTypes);
                }
            }
        }
        else
        {
            // Service: 从类本身的 Singleton 特性收集
            try
            {
                var attr = service.Symbol.GetAttribute(symbols.SingletonAttribute);

                if (attr != null)
                {
                    foreach (var arg in attr.ConstructorArguments)
                    {
                        if (arg.Kind == TypedConstantKind.Array)
                        {
                            foreach (var item in arg.Values)
                            {
                                if (item.Value is INamedTypeSymbol type)
                                    builder.Add(type);
                            }
                        }
                    }
                }

                // 如果没有显式指定暴露类型，默认暴露自身
                if (builder.Count == 0)
                {
                    builder.Add(service.Symbol);
                }
            }
            catch
            {
                // 如果获取失败，至少返回服务本身的类型
                if (builder.Count == 0)
                {
                    builder.Add(service.Symbol);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static void ValidateScopeServices(
        ValidatedTypeInfo scope,
        ImmutableArray<INamedTypeSymbol> services,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var type in services)
        {
            // 检查是否是 Service 或 Provider
            var isService = type.HasAttribute(symbols.SingletonAttribute);
            var isProvider = type.HasAttribute(symbols.ProviderAttribute);

            if (!isService && !isProvider)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeModulesServiceMustBeService,
                        scope.Location,
                        scope.Symbol.Name,
                        type.ToDisplayString()
                    )
                );
            }
        }
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
}
