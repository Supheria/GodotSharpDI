using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class ServiceGraphBuilder
{
    public static ServiceGraph Build(ImmutableArray<INamedTypeSymbol> types, SymbolCache symbolCache)
    {
        var graph = new ServiceGraph();

        if (types.IsDefaultOrEmpty)
        {
            return graph;
        }

        // 去重，避免因为 partial 等原因导致重复分析
        var uniqueTypes = new HashSet<INamedTypeSymbol>(
            types.Where(t => t is not null),
            SymbolEqualityComparer.Default
        );

        foreach (var type in uniqueTypes)
        {
            // 角色判定
            var isHost = SymbolHelper.HasAttribute(type, symbolCache.HostAttribute);
            var isUser = SymbolHelper.HasAttribute(type, symbolCache.UserAttribute);
            var isScope = SymbolHelper.ImplementsInterface(type, symbolCache.ScopeInterface);

            // 生命周期 Attribute 判定（Singleton / Transient）
            var serviceInfo = ServiceScanner.Analyze(type, symbolCache);

            //
            // 规则 A：类型可以同时是 Host 和 User
            // 规则 B：类型不能同时是 Scope 又是 Host 或 User
            // 规则 C：Scope / Host / User 不能带 SingletonService / TransientService
            //
            // 注意：诊断在独立的 Analyzer 项目中做，这里只“忽略不合法类型”
            //

            // Scope + Host/User → 生成器忽略（Analyzer 负责报错）
            if (isScope && (isHost || isUser))
            {
                continue;
            }

            // Host/User/Scope 上如果带了 Service Attribute → 生成器忽略（Analyzer 报错）
            if (serviceInfo is not null && (isHost || isUser || isScope))
            {
                continue;
            }

            // 1. 普通 Service（带 Singleton/Transient Attribute，且不是 Host/User/Scope）
            if (serviceInfo is not null)
            {
                AddServiceFromServiceType(type, serviceInfo, graph);
                continue;
            }

            // 2. Scope（只能单独作为 Scope）
            if (isScope)
            {
                var scope = ScopeScanner.Analyze(type, symbolCache);
                graph.Scopes.Add(scope);
                AddEdgesFromScope(scope, graph);
                continue;
            }

            // 3. Host / User（允许 Host+User 叠加）
            if (isHost)
            {
                var host = HostScanner.Analyze(type, symbolCache);
                graph.Hosts.Add(host);
                AddServicesFromHost(host, graph);
            }

            if (isUser)
            {
                var user = UserScanner.Analyze(type, symbolCache);
                graph.Users.Add(user);
                AddEdgesFromUser(user, graph);
            }

            // 其他类型（既不是 Service，也不是 Host/User/Scope）→ 忽略
        }

        // 未来：在这里对 graph 进行纯结构级处理（循环依赖、未解析服务等）
        // 诊断由单独 Analyzer 负责，这里只构建图

        return graph;
    }

    // --------------------------------------------------
    //  Service 收集
    // --------------------------------------------------

    private static void AddServiceFromServiceType(
        INamedTypeSymbol implementation,
        ServiceTypeInfo info,
        ServiceGraph graph
    )
    {
        var lifetime = info.IsSingleton ? ServiceLifetime.Singleton : ServiceLifetime.Transient;

        foreach (var exposed in info.ExposedServiceTypes)
        {
            var isImplicit = SymbolEqualityComparer.Default.Equals(exposed, implementation);
            var descriptor = new ServiceDescriptor(
                serviceType: exposed,
                implementationType: implementation,
                lifetime: lifetime,
                providedByHost: false,
                providedByScope: false,
                isImplicit: isImplicit
            );
            AddServiceDescriptor(graph, descriptor);
        }
    }

    private static void AddServicesFromHost(HostDescriptor host, ServiceGraph graph)
    {
        foreach (var (_, serviceType) in host.SingletonServices)
        {
            // Host 提供的必然是 Singleton
            var descriptor = new ServiceDescriptor(
                serviceType: serviceType,
                implementationType: host.HostType,
                lifetime: ServiceLifetime.Singleton,
                providedByHost: true,
                providedByScope: false,
                isImplicit: false
            );
            AddServiceDescriptor(graph, descriptor);

            // 依赖图：服务类型 → Host 类型（消费/提供该服务）
            AddEdge(graph, serviceType, host.HostType);
        }
    }

    // --------------------------------------------------
    //  User / Scope 边信息
    // --------------------------------------------------

    private static void AddEdgesFromUser(UserDescriptor user, ServiceGraph graph)
    {
        foreach (var (_, depType) in user.Dependencies)
        {
            AddEdge(graph, depType, user.UserType);
        }
    }

    private static void AddEdgesFromScope(ScopeDescriptor scope, ServiceGraph graph)
    {
        // Scope.Instantiate：这个 Scope 会主动创建这些实现类型
        foreach (var impl in scope.Instantiate)
        {
            AddEdge(graph, impl, scope.ScopeType);
        }

        // Scope.Expect：这个 Scope 依赖这些 Host 提供的服务
        foreach (var hostType in scope.Expect)
        {
            AddEdge(graph, hostType, scope.ScopeType);
        }
    }

    // --------------------------------------------------
    //  公共辅助：添加 ServiceDescriptor / Edge
    // --------------------------------------------------

    private static void AddServiceDescriptor(ServiceGraph graph, ServiceDescriptor descriptor)
    {
        graph.Services.Add(descriptor);

        if (
            !graph.ServicesByImplementation.TryGetValue(descriptor.ImplementationType, out var list)
        )
        {
            list = new List<ServiceDescriptor>();
            graph.ServicesByImplementation[descriptor.ImplementationType] = list;
        }

        list.Add(descriptor);
    }

    private static void AddEdge(
        ServiceGraph graph,
        INamedTypeSymbol serviceOrImpl,
        INamedTypeSymbol consumer
    )
    {
        if (!graph.Edges.TryGetValue(serviceOrImpl, out var list))
        {
            list = new List<INamedTypeSymbol>();
            graph.Edges[serviceOrImpl] = list;
        }

        list.Add(consumer);
    }
}
