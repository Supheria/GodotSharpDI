using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharpDI.Generator.Internal.Data.TypeInfo;

namespace GodotSharpDI.Generator.Internal.DiBuild;

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
        var hosts = validTypes.Where(t => t.Role == TypeRole.Host).ToImmutableArray();
        var users = validTypes.Where(t => t.Role == TypeRole.User).ToImmutableArray();
        var hostAndUsers = validTypes.Where(t => t.Role == TypeRole.HostAndUser).ToImmutableArray();
        var scopes = validTypes.Where(t => t.Role == TypeRole.Scope).ToImmutableArray();

        // 构建服务提供映射（带冲突检测）
        var (serviceProviders, providerDiags) = BuildServiceProviderMap(
            services,
            hosts,
            hostAndUsers,
            symbols
        );
        diagnostics.AddRange(providerDiags);

        // 构建节点
        var (serviceNodes, serviceDiags) = BuildServiceNodes(services, serviceProviders, symbols);
        diagnostics.AddRange(serviceDiags);

        var (hostNodes, hostDiags) = BuildHostNodes(hosts);
        diagnostics.AddRange(hostDiags);

        var (userNodes, userDiags) = BuildUserNodes(users);
        diagnostics.AddRange(userDiags);

        var (hostAndUserNodes, hostAndUserDiags) = BuildHostAndUserNodes(hostAndUsers);
        diagnostics.AddRange(hostAndUserDiags);

        var (scopeNodes, scopeDiags) = BuildScopeNodes(scopes, serviceProviders, symbols);
        diagnostics.AddRange(scopeDiags);

        // 验证 Host 服务引用
        var hostServiceDiags = ValidateHostServices(hosts, hostAndUsers, serviceProviders);
        diagnostics.AddRange(hostServiceDiags);

        // 依赖图验证（包含所有 User 类型）
        var allUserNodes = userNodes.Concat(hostAndUserNodes).ToImmutableArray();
        var graphDiags = ValidateDependencyGraph(
            serviceNodes,
            allUserNodes,
            serviceProviders,
            symbols
        );
        diagnostics.AddRange(graphDiags);

        // 构建Service节点映射
        var serviceNodeMapBuilder = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in serviceNodes)
        {
            serviceNodeMapBuilder[node.TypeInfo.Symbol] = node;
        }

        // 构建Host节点映射
        var hostNodeMapBuilder = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in hostNodes)
        {
            hostNodeMapBuilder[node.TypeInfo.Symbol] = node;
        }

        // 构建HostAndUser节点映射
        var hostAndUserNodeMapBuilder = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in hostAndUserNodes)
        {
            hostAndUserNodeMapBuilder[node.TypeInfo.Symbol] = node;
        }

        var graph = new DiGraph(
            ServiceNodes: serviceNodes,
            HostNodes: hostNodes,
            UserNodes: userNodes,
            HostAndUserNodes: hostAndUserNodes,
            ScopeNodes: scopeNodes,
            ServiceNodeMap: serviceNodeMapBuilder.ToImmutable(),
            HostNodeMap: hostNodeMapBuilder.ToImmutable(),
            HostAndUserNodeMap: hostAndUserNodeMapBuilder.ToImmutable()
        );

        return new DiGraphBuildResult(graph, diagnostics.ToImmutable());
    }

    private static (
        Dictionary<ITypeSymbol, TypeInfo>,
        ImmutableArray<Diagnostic>
    ) BuildServiceProviderMap(
        ImmutableArray<TypeInfo> services,
        ImmutableArray<TypeInfo> hosts,
        ImmutableArray<TypeInfo> hostAndUsers,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var map = new Dictionary<ITypeSymbol, TypeInfo>(SymbolEqualityComparer.Default);
        var conflictTracker = new Dictionary<ITypeSymbol, List<string>>(
            SymbolEqualityComparer.Default
        );

        void AddProvider(ITypeSymbol exposedType, TypeInfo provider, string providerDescription)
        {
            if (!map.TryGetValue(exposedType, out var existing))
            {
                map[exposedType] = provider;
                return;
            }

            if (!conflictTracker.TryGetValue(exposedType, out var conflicts))
            {
                conflicts = new List<string> { existing.Symbol.ToDisplayString() };
                conflictTracker[exposedType] = conflicts;
            }
            conflicts.Add(providerDescription);
        }

        // Service 提供的服务
        foreach (var service in services)
        {
            var exposedTypes = GetServiceExposedTypes(service, symbols);
            foreach (var exposedType in exposedTypes)
            {
                AddProvider(exposedType, service, service.Symbol.ToDisplayString());
            }
        }

        // Host 提供的服务
        foreach (var host in hosts.Concat(hostAndUsers))
        {
            foreach (var member in host.Members)
            {
                if (member.IsSingletonMember)
                {
                    foreach (var exposedType in member.ExposedTypes)
                    {
                        var providerDesc = $"{host.Symbol.ToDisplayString()}.{member.Symbol.Name}";
                        AddProvider(exposedType, host, providerDesc);
                    }
                }
            }
        }

        // 报告所有冲突
        foreach (var conflict in conflictTracker)
        {
            var providers = string.Join(", ", conflict.Value);
            diagnostics.Add(
                DiagnosticBuilder.CreateAtNone(
                    DiagnosticDescriptors.ServiceTypeConflict,
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

        var attr = service.Symbol.GetAttribute(symbols.SingletonAttribute);

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

        if (builder.Count == 0)
        {
            builder.Add(service.Symbol);
        }

        return builder.ToImmutable();
    }

    private static (ImmutableArray<TypeNode>, ImmutableArray<Diagnostic>) BuildServiceNodes(
        ImmutableArray<TypeInfo> services,
        Dictionary<ITypeSymbol, TypeInfo> serviceProviders,
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

            foreach (var member in host.Members)
            {
                if (member.IsSingletonMember)
                {
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

    private static (ImmutableArray<TypeNode>, ImmutableArray<Diagnostic>) BuildHostAndUserNodes(
        ImmutableArray<TypeInfo> hostAndUsers
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var hostAndUser in hostAndUsers)
        {
            var providedServices = ImmutableArray.CreateBuilder<ITypeSymbol>();
            var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();

            foreach (var member in hostAndUser.Members)
            {
                if (member.IsSingletonMember)
                {
                    providedServices.AddRange(member.ExposedTypes);
                }
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

            nodes.Add(
                new TypeNode(
                    TypeInfo: hostAndUser,
                    Dependencies: dependencies.ToImmutable(),
                    ProvidedServices: providedServices.ToImmutable()
                )
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    private static (ImmutableArray<ScopeNode>, ImmutableArray<Diagnostic>) BuildScopeNodes(
        ImmutableArray<TypeInfo> scopes,
        Dictionary<ITypeSymbol, TypeInfo> serviceProviders,
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

            // 验证 Services
            foreach (var type in services)
            {
                var hasLifetime = type.HasAttribute(symbols.SingletonAttribute);

                if (!hasLifetime)
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

            // 验证 Hosts
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

            if (services.IsEmpty)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeModulesServicesEmpty,
                        scope.Location,
                        scope.Symbol.Name
                    )
                );
            }

            if (hosts.IsEmpty)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeModulesHostsEmpty,
                        scope.Location,
                        scope.Symbol.Name
                    )
                );
            }

            nodes.Add(
                new ScopeNode(TypeInfo: scope, InstantiateServices: services, ExpectHosts: hosts)
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    private static ImmutableArray<Diagnostic> ValidateHostServices(
        ImmutableArray<TypeInfo> hosts,
        ImmutableArray<TypeInfo> hostAndUsers,
        Dictionary<ITypeSymbol, TypeInfo> serviceProviders
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateDependencyGraph(
        ImmutableArray<TypeNode> serviceNodes,
        ImmutableArray<TypeNode> allUserNodes,
        Dictionary<ITypeSymbol, TypeInfo> serviceProviders,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        var serviceImplToNode = new Dictionary<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in serviceNodes)
        {
            serviceImplToNode[node.TypeInfo.Symbol] = node;
        }

        // 检查循环依赖（使用优化的 Tarjan 算法）
        var circularDependencyDetector = new CircularDependencyDetector(
            serviceImplToNode,
            serviceProviders
        );
        var circularDiagnostics = circularDependencyDetector.DetectCircularDependencies();
        diagnostics.AddRange(circularDiagnostics);

        // 检查 Service 构造函数参数
        foreach (var node in serviceNodes)
        {
            if (node.TypeInfo.Constructor != null)
            {
                foreach (var param in node.TypeInfo.Constructor.Parameters)
                {
                    if (!serviceProviders.ContainsKey(param.Type))
                    {
                        diagnostics.Add(
                            DiagnosticBuilder.Create(
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

        // 检查所有 User（包括 HostAndUser）的 Inject 成员
        foreach (var node in allUserNodes)
        {
            foreach (var dep in node.Dependencies)
            {
                if (dep.Source == DependencySource.InjectMember)
                {
                    if (!serviceProviders.ContainsKey(dep.TargetType))
                    {
                        diagnostics.Add(
                            DiagnosticBuilder.Create(
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
}
