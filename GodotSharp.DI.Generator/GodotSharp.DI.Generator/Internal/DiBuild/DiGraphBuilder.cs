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

        // 构建服务提供映射
        var serviceProviders = BuildServiceProviderMap(services, hosts, symbols);

        // 构建节点
        var (serviceNodes, serviceDiags) = BuildServiceNodes(services, serviceProviders, symbols);
        diagnostics.AddRange(serviceDiags);

        var (hostNodes, hostDiags) = BuildHostNodes(hosts);
        diagnostics.AddRange(hostDiags);

        var (userNodes, userDiags) = BuildUserNodes(users);
        diagnostics.AddRange(userDiags);

        var (scopeNodes, scopeDiags) = BuildScopeNodes(scopes, serviceProviders, symbols);
        diagnostics.AddRange(scopeDiags);

        // 依赖图验证
        var graphDiags = ValidateDependencyGraph(serviceNodes, serviceProviders, symbols);
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

    private static Dictionary<
        ITypeSymbol,
        (TypeInfo Provider, ServiceLifetime Lifetime)
    > BuildServiceProviderMap(
        ImmutableArray<TypeInfo> services,
        ImmutableArray<TypeInfo> hosts,
        CachedSymbols symbols
    )
    {
        var map = new Dictionary<ITypeSymbol, (TypeInfo, ServiceLifetime)>(
            SymbolEqualityComparer.Default
        );

        // Service 提供的服务
        foreach (var service in services)
        {
            var exposedTypes = GetServiceExposedTypes(service, symbols);
            foreach (var exposedType in exposedTypes)
            {
                map[exposedType] = (service, service.Lifetime);
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
                        map[exposedType] = (host, ServiceLifetime.Singleton);
                    }
                }
            }
        }

        return map;
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

        // 如果没有指定，使用类型本身
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

            var instantiate = scope.ModulesInfo.Instantiate;
            var expect = scope.ModulesInfo.Expect;

            // 验证 Instantiate - 检查类型是否有 Singleton 或 Transient 特性
            foreach (var type in instantiate)
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
                            DiagnosticDescriptors.ScopeInstantiateMustBeService,
                            scope.Location,
                            type.ToDisplayString()
                        )
                    );
                }
            }

            nodes.Add(
                new ScopeNode(
                    TypeInfo: scope,
                    InstantiateServices: instantiate,
                    ExpectHosts: expect,
                    AllProvidedServices: ImmutableArray<ITypeSymbol>.Empty
                )
            );
        }

        return (nodes.ToImmutable(), diagnostics.ToImmutable());
    }

    private static ImmutableArray<Diagnostic> ValidateDependencyGraph(
        ImmutableArray<TypeNode> serviceNodes,
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
