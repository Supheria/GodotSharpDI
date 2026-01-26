using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal static class DiGraphBuilder
{
    public static DiGraphBuildResult Build(
        ImmutableArray<ClassValidationResult> classResults,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        var validTypes = classResults
            .Where(r => r.TypeInfo != null)
            .Select(r => r.TypeInfo!)
            .ToImmutableArray();

        if (validTypes.IsEmpty)
            return DiGraphBuildResult.Empty;

        // 按角色分类
        var services = validTypes.Where(t => t.Role == TypeRole.Service).ToImmutableArray();
        var hosts = validTypes
            .Where(t => t.Role == TypeRole.Host || t.Role == TypeRole.HostAndUser)
            .ToImmutableArray();
        var users = validTypes
            .Where(t => t.Role == TypeRole.User || t.Role == TypeRole.HostAndUser)
            .ToImmutableArray();
        var scopes = validTypes.Where(t => t.Role == TypeRole.Scope).ToImmutableArray();

        // 构建服务提供映射（带冲突检测）
        var (serviceProviders, providerDiags) = BuildServiceProviderMap(services, hosts, symbols);
        diagnostics.AddRange(providerDiags);

        // 构建节点
        var (serviceNodes, serviceDiags) = BuildServiceNodes(services, serviceProviders, symbols);
        diagnostics.AddRange(serviceDiags);

        var (hostNodes, hostDiags) = BuildHostNodes(hosts);
        diagnostics.AddRange(hostDiags);

        var (userNodes, userDiags) = BuildUserNodes(users);
        diagnostics.AddRange(userDiags);

        var (scopeNodes, scopeDiags) = BuildScopeNodes(scopes, serviceProviders, symbols);
        diagnostics.AddRange(scopeDiags);

        // 验证 Host 服务引用
        var hostServiceDiags = ValidateHostServices(hosts, serviceProviders);
        diagnostics.AddRange(hostServiceDiags);

        // 依赖图验证（修正：传入 userNodes）
        var graphDiags = ValidateDependencyGraph(
            serviceNodes,
            userNodes,
            serviceProviders,
            symbols
        );
        diagnostics.AddRange(graphDiags);

        // 构建类型映射 - 使用 TypeNode 而不是 TypeInfo
        var typeMapBuilder = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );

        foreach (var node in serviceNodes)
            typeMapBuilder[(ITypeSymbol)node.TypeInfo.Symbol] = node;
        foreach (var node in hostNodes)
            typeMapBuilder[(ITypeSymbol)node.TypeInfo.Symbol] = node;
        foreach (var node in userNodes)
            typeMapBuilder[(ITypeSymbol)node.TypeInfo.Symbol] = node;

        var graph = new DiGraph(
            ServiceNodes: serviceNodes,
            HostNodes: hostNodes,
            UserNodes: userNodes,
            ScopeNodes: scopeNodes,
            TypeMap: typeMapBuilder.ToImmutable()
        );

        return new DiGraphBuildResult(graph, diagnostics.ToImmutable());
    }

    private static (
        Dictionary<ITypeSymbol, (TypeInfo Provider, ServiceLifetime Lifetime)>,
        ImmutableArray<Diagnostic>
    ) BuildServiceProviderMap(
        ImmutableArray<TypeInfo> services,
        ImmutableArray<TypeInfo> hosts,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var map = new Dictionary<ITypeSymbol, (TypeInfo, ServiceLifetime)>(
            SymbolEqualityComparer.Default
        );
        // 用于跟踪冲突的提供者
        var conflictTracker = new Dictionary<ITypeSymbol, List<string>>(
            SymbolEqualityComparer.Default
        );

        void AddProvider(
            ITypeSymbol exposedType,
            TypeInfo provider,
            ServiceLifetime lifetime,
            string providerDescription
        )
        {
            if (map.ContainsKey(exposedType))
            {
                // 存在冲突
                if (!conflictTracker.ContainsKey(exposedType))
                {
                    // 添加第一个提供者到冲突列表
                    var existingProvider = map[exposedType].Item1;
                    conflictTracker[exposedType] = new List<string>
                    {
                        existingProvider.Symbol.ToDisplayString(),
                    };
                }
                conflictTracker[exposedType].Add(providerDescription);
            }
            else
            {
                map[exposedType] = (provider, lifetime);
            }
        }

        // Service 提供的服务
        foreach (var service in services)
        {
            var exposedTypes = GetServiceExposedTypes(service, symbols);
            foreach (var exposedType in exposedTypes)
            {
                AddProvider(
                    exposedType,
                    service,
                    service.Lifetime,
                    service.Symbol.ToDisplayString()
                );
            }
        }

        // Host 提供的服务
        foreach (var host in hosts)
        {
            foreach (var member in host.Members)
            {
                if (
                    member.Kind == MemberKind.SingletonField
                    || member.Kind == MemberKind.SingletonProperty
                )
                {
                    foreach (var exposedType in member.ExposedTypes)
                    {
                        var providerDesc = $"{host.Symbol.ToDisplayString()}.{member.Symbol.Name}";
                        AddProvider(exposedType, host, ServiceLifetime.Singleton, providerDesc);
                    }
                }
            }
        }

        // 报告所有冲突
        foreach (var conflict in conflictTracker)
        {
            var providers = string.Join(", ", conflict.Value);
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.ServiceTypeConflict,
                    Location.None,
                    conflict.Key.ToDisplayString(),
                    providers
                )
            );
        }

        return (map, diagnostics.ToImmutable());
    }

    private static ImmutableArray<ITypeSymbol> GetServiceExposedTypes(
        TypeInfo service,
        CachedSymbols symbols
    )
    {
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

        var singletonAttr = service
            .Symbol.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SingletonAttribute)
            );

        var transientAttr = service
            .Symbol.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.TransientAttribute)
            );

        var attr = singletonAttr ?? transientAttr;
        if (attr != null)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in arg.Values)
                    {
                        if (item.Value is ITypeSymbol type)
                            builder.Add(type);
                    }
                }
            }
        }

        // 如果没有指定,使用类型本身
        if (builder.Count == 0)
        {
            builder.Add(service.Symbol);
        }

        return builder.ToImmutable();
    }

    private static (ImmutableArray<TypeNode>, ImmutableArray<Diagnostic>) BuildServiceNodes(
        ImmutableArray<TypeInfo> services,
        Dictionary<ITypeSymbol, (TypeInfo Provider, ServiceLifetime Lifetime)> serviceProviders,
        CachedSymbols symbols
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var service in services)
        {
            var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();

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

            var providedServices = GetServiceExposedTypes(service, symbols);

            nodes.Add(
                new TypeNode(
                    TypeInfo: service,
                    Dependencies: dependencies.ToImmutable(),
                    ProvidedServices: providedServices
                )
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    private static (ImmutableArray<TypeNode>, ImmutableArray<Diagnostic>) BuildHostNodes(
        ImmutableArray<TypeInfo> hosts
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var host in hosts)
        {
            var providedServices = ImmutableArray.CreateBuilder<ITypeSymbol>();

            // 收集 Host 成员上标记的 [Singleton] 暴露的服务类型
            foreach (var member in host.Members)
            {
                if (
                    member.Kind == MemberKind.SingletonField
                    || member.Kind == MemberKind.SingletonProperty
                )
                {
                    // ExposedTypes 包含了 [Singleton(typeof(IXxx))] 中指定的接口类型
                    providedServices.AddRange(member.ExposedTypes);
                }
            }

            nodes.Add(
                new TypeNode(
                    TypeInfo: host,
                    Dependencies: ImmutableArray<DependencyEdge>.Empty,
                    ProvidedServices: providedServices.ToImmutable()
                )
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    private static (ImmutableArray<TypeNode>, ImmutableArray<Diagnostic>) BuildUserNodes(
        ImmutableArray<TypeInfo> users
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var user in users)
        {
            var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();

            foreach (var member in user.Members)
            {
                if (
                    member.Kind == MemberKind.InjectField
                    || member.Kind == MemberKind.InjectProperty
                )
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

            nodes.Add(
                new TypeNode(
                    TypeInfo: user,
                    Dependencies: dependencies.ToImmutable(),
                    ProvidedServices: ImmutableArray<ITypeSymbol>.Empty
                )
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    private static (ImmutableArray<ScopeNode>, ImmutableArray<Diagnostic>) BuildScopeNodes(
        ImmutableArray<TypeInfo> scopes,
        Dictionary<ITypeSymbol, (TypeInfo Provider, ServiceLifetime Lifetime)> serviceProviders,
        CachedSymbols symbols
    )
    {
        var nodes = ImmutableArray.CreateBuilder<ScopeNode>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var scope in scopes)
        {
            if (scope.ModulesInfo == null)
                continue;

            var services = scope.ModulesInfo.Services;
            var hosts = scope.ModulesInfo.Hosts;

            // 验证 Services - 检查类型是否有 Singleton 或 Transient 特性
            foreach (var type in services)
            {
                var hasLifetime = type.GetAttributes()
                    .Any(attr =>
                    {
                        var attrClass = attr.AttributeClass;
                        if (attrClass == null)
                            return false;

                        return SymbolEqualityComparer.Default.Equals(
                                attrClass,
                                symbols.SingletonAttribute
                            )
                            || SymbolEqualityComparer.Default.Equals(
                                attrClass,
                                symbols.TransientAttribute
                            );
                    });

                if (!hasLifetime)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ScopeModulesServiceMustBeService,
                            scope.Location,
                            type.ToDisplayString()
                        )
                    );
                }
            }

            // 验证 Hosts - 检查类型是否有 Host 特性
            foreach (var type in hosts)
            {
                var isHost = type.GetAttributes()
                    .Any(attr =>
                    {
                        var attrClass = attr.AttributeClass;
                        if (attrClass == null)
                            return false;

                        return SymbolEqualityComparer.Default.Equals(
                            attrClass,
                            symbols.HostAttribute
                        );
                    });

                if (!isHost)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ScopeModulesHostMustBeHost,
                            scope.Location,
                            scope.Symbol.Name,
                            type.ToDisplayString()
                        )
                    );
                }
            }

            // 检查 Instantiate 是否为空 (Info 诊断)
            if (services.IsEmpty)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.ScopeModulesServicesEmpty,
                        scope.Location,
                        scope.Symbol.Name
                    )
                );
            }

            // 检查 Expect 是否为空 (Info 诊断)
            if (hosts.IsEmpty)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.ScopeModulesHostsEmpty,
                        scope.Location,
                        scope.Symbol.Name
                    )
                );
            }

            nodes.Add(
                new ScopeNode(
                    TypeInfo: scope,
                    InstantiateServices: services,
                    ExpectHosts: hosts,
                    AllProvidedServices: ImmutableArray<ITypeSymbol>.Empty
                )
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    /// <summary>
    /// 验证 Host 暴露的服务类型
    /// </summary>
    private static ImmutableArray<Diagnostic> ValidateHostServices(
        ImmutableArray<TypeInfo> hosts,
        Dictionary<ITypeSymbol, (TypeInfo Provider, ServiceLifetime Lifetime)> serviceProviders
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // 注意：Host暴露的服务不需要在serviceProviders中预先注册
        // Host自身就是服务的提供者，它通过成员上的[Singleton]特性暴露服务
        // 这里的验证主要是确保Host暴露的类型是合理的（但这已经在ClassPipeline中完成）

        // 由于当前设计中，Host可以暴露任意类型（只要不是已标记为Service的类型）
        // 并且这个检查已经在ClassPipeline.ProcessSingleMember中通过
        // DiagnosticDescriptors.HostSingletonMemberIsServiceType 完成
        // 因此这里不需要额外的验证逻辑

        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateDependencyGraph(
        ImmutableArray<TypeNode> serviceNodes,
        ImmutableArray<TypeNode> userNodes,
        Dictionary<ITypeSymbol, (TypeInfo Provider, ServiceLifetime Lifetime)> serviceProviders,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // 检查循环依赖
        foreach (var node in serviceNodes)
        {
            var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var path = new Stack<ITypeSymbol>();

            if (HasCircularDependency(node.TypeInfo.Symbol, serviceProviders, visited, path))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.CircularDependencyDetected,
                        node.TypeInfo.Location,
                        string.Join(" -> ", path.Reverse().Select(t => t.ToDisplayString()))
                    )
                );
            }
        }

        // 检查 Singleton 依赖 Transient
        foreach (var node in serviceNodes)
        {
            if (node.TypeInfo.Lifetime == ServiceLifetime.Singleton)
            {
                foreach (var dep in node.Dependencies)
                {
                    if (serviceProviders.TryGetValue(dep.TargetType, out var provider))
                    {
                        if (provider.Lifetime == ServiceLifetime.Transient)
                        {
                            diagnostics.Add(
                                Diagnostic.Create(
                                    DiagnosticDescriptors.SingletonCannotDependOnTransient,
                                    dep.Location,
                                    node.TypeInfo.Symbol.ToDisplayString(),
                                    dep.TargetType.ToDisplayString()
                                )
                            );
                        }
                    }
                }
            }
        }

        // 修正：检查 Service 构造函数参数是否是已暴露的服务类型
        foreach (var node in serviceNodes)
        {
            if (node.TypeInfo.Constructor != null)
            {
                foreach (var param in node.TypeInfo.Constructor.Parameters)
                {
                    // 检查参数类型（通常是接口）是否在 serviceProviders 的键中
                    // serviceProviders 的键是暴露的服务类型（接口），值是提供者
                    if (!serviceProviders.ContainsKey(param.Type))
                    {
                        diagnostics.Add(
                            Diagnostic.Create(
                                DiagnosticDescriptors.ServiceConstructorParameterInvalid,
                                param.Location,
                                node.TypeInfo.Symbol.Name,
                                param.Type.ToDisplayString()
                            )
                        );
                    }
                }
            }
        }

        // 新增：检查 User 的 Inject 成员是否是已暴露的服务类型
        foreach (var node in userNodes)
        {
            foreach (var dep in node.Dependencies)
            {
                // 只检查来自 InjectMember 的依赖
                if (dep.Source == DependencySource.InjectMember)
                {
                    // 检查依赖的类型是否在 serviceProviders 中
                    if (!serviceProviders.ContainsKey(dep.TargetType))
                    {
                        diagnostics.Add(
                            Diagnostic.Create(
                                DiagnosticDescriptors.InjectMemberInvalidType,
                                dep.Location,
                                node.TypeInfo.Symbol.Name,
                                dep.TargetType.ToDisplayString()
                            )
                        );
                    }
                }
            }
        }

        return diagnostics.ToImmutable();
    }

    private static bool HasCircularDependency(
        ITypeSymbol current,
        Dictionary<ITypeSymbol, (TypeInfo Provider, ServiceLifetime Lifetime)> serviceProviders,
        HashSet<ITypeSymbol> visited,
        Stack<ITypeSymbol> path
    )
    {
        if (path.Contains(current, SymbolEqualityComparer.Default))
            return true;

        if (visited.Contains(current))
            return false;

        visited.Add(current);
        path.Push(current);

        if (serviceProviders.TryGetValue(current, out var provider))
        {
            if (provider.Provider.Constructor != null)
            {
                foreach (var param in provider.Provider.Constructor.Parameters)
                {
                    if (HasCircularDependency(param.Type, serviceProviders, visited, path))
                        return true;
                }
            }
        }

        path.Pop();
        return false;
    }
}
