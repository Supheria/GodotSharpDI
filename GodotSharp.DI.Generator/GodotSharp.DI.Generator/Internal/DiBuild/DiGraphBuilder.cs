using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Extensions;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal sealed class DiGraphBuilder
{
    private readonly ImmutableArray<ClassType> _allTypes;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;
    private readonly Dictionary<INamedTypeSymbol, ClassTypeInfo> _typeInfoMap;

    public DiGraphBuilder(ImmutableArray<ClassType> allTypes, CachedSymbols symbols)
    {
        _allTypes = allTypes;
        _symbols = symbols;
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        _typeInfoMap = new Dictionary<INamedTypeSymbol, ClassTypeInfo>(
            SymbolEqualityComparer.Default
        );
    }

    private bool HasErrors()
    {
        return _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    public DiGraphBuildResult Build()
    {
        // 阶段 1: 构建基础 TypeInfoMap（带类型级别验证）
        BuildTypeInfoMap();
        if (HasErrors())
        {
            return DiGraphBuildResult.Failure(_diagnostics.ToImmutable());
        }

        // 阶段 2: 构建 ServiceGraph
        var graph = BuildDiGraph();

        // 阶段 3: 图级别验证
        ValidateServiceGraph(graph);
        if (HasErrors())
        {
            return DiGraphBuildResult.Failure(_diagnostics.ToImmutable());
        }

        return new DiGraphBuildResult(graph, _diagnostics.ToImmutable());
    }

    // ============================================================
    // 阶段 1: 构建 TypeInfoMap
    // ============================================================

    private void BuildTypeInfoMap()
    {
        foreach (var type in _allTypes)
        {
            var result = ClassTypeInfoFactory.Create(type, _symbols);
            _diagnostics.AddRange(result.Diagnostics);
            if (result.TypeInfo is not null)
            {
                _typeInfoMap.Add(result.TypeInfo.Symbol, result.TypeInfo);
            }
        }
    }

    // ============================================================
    // 阶段 2: 构建 ServiceGraph
    // ============================================================

    private DiGraph BuildDiGraph()
    {
        var services = ImmutableArray.CreateBuilder<ServiceInfo>();
        var hostOrUsers = ImmutableArray.CreateBuilder<HostUserInfo>();
        var scopes = ImmutableArray.CreateBuilder<ScopeInfo>();

        foreach (var typeInfo in _typeInfoMap.Values)
        {
            if (typeInfo.IsService)
            {
                services.Add(typeInfo.GetServiceInfo());
            }
            else if (typeInfo.IsHost || typeInfo.IsUser)
            {
                hostOrUsers.Add(typeInfo.GetHostUserInfo());
            }
            else if (typeInfo.IsScope)
            {
                var scopeInfo = CreateScopeInfo(typeInfo);
                if (scopeInfo is not null)
                {
                    scopes.Add(scopeInfo);
                }
            }
        }

        return new DiGraph(
            Services: services.ToImmutable(),
            HostOrUsers: hostOrUsers.ToImmutable(),
            Scopes: scopes.ToImmutable()
        );
    }

    private ScopeInfo? CreateScopeInfo(ClassTypeInfo scopeInfo)
    {
        if (scopeInfo.Modules is not null)
        {
            var services = CollectScopeModules(scopeInfo, scopeInfo.Modules);
            return new ScopeInfo(scopeInfo.Symbol, scopeInfo.Namespace, services);
        }

        if (scopeInfo.AutoModules is not null)
        {
            var services = CollectScopeModulesAuto(scopeInfo);
            return new ScopeInfo(scopeInfo.Symbol, scopeInfo.Namespace, services);
        }

        var diagnostic = Diagnostic.Create(
            descriptor: DiagnosticDescriptors.UnexpectScopeWithoutModules,
            location: scopeInfo.DeclarationSyntax.GetLocation(),
            scopeInfo.Symbol.Name
        );
        _diagnostics.Add(diagnostic);
        return null;
    }

    private ImmutableArray<ScopeServiceDescriptor> CollectScopeModules(
        ClassTypeInfo scopeInfo,
        AttributeData modules
    )
    {
        var builder = ImmutableArray.CreateBuilder<ScopeServiceDescriptor>();
        var location = scopeInfo.DeclarationSyntax.GetLocation();

        var instantiateValues = modules.GetNamedArgument("Instantiate");
        if (instantiateValues.Length < 1)
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.ScopeModulesInstantiateEmpty, // 没有指定任何 Instantiate
                location: location,
                scopeInfo.Symbol.Name
            );
            _diagnostics.Add(diagnostic);
        }
        else
        {
            foreach (var t in instantiateValues)
            {
                var implType = (INamedTypeSymbol)t.Value!;
                if (!_typeInfoMap.TryGetValue(implType, out var implInfo) || !implInfo.IsService) // 未定义的服务类型
                {
                    var diagnostic = Diagnostic.Create(
                        descriptor: DiagnosticDescriptors.ScopeServiceNotDefined,
                        location: location,
                        scopeInfo.Symbol.Name,
                        implType.Name
                    );
                    _diagnostics.Add(diagnostic);
                    continue;
                }

                var service = new ScopeServiceDescriptor(
                    ImplementType: implType,
                    ExposedServiceTypes: implInfo.ServiceExposedTypes,
                    Lifetime: implInfo.ServiceLifetime,
                    IsHostProvided: false
                );
                builder.Add(service);
            }
        }

        var expectValues = modules.GetNamedArgument("Expect");
        if (expectValues.Length < 1)
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.ScopeModulesExpectEmpty, // 没有指定任何 Expect
                location,
                scopeInfo.Symbol.Name
            );
            _diagnostics.Add(diagnostic);
        }
        else
        {
            foreach (var t in expectValues)
            {
                var hostType = (INamedTypeSymbol)t.Value!;
                if (!_typeInfoMap.TryGetValue(hostType, out var hostInfo) || !hostInfo.IsHost) // Expect 非 Host 类型
                {
                    var diagnostic = Diagnostic.Create(
                        descriptor: DiagnosticDescriptors.ScopeExpectNotHost,
                        location,
                        scopeInfo.Symbol.Name,
                        hostType.Name
                    );
                    _diagnostics.Add(diagnostic);
                    continue;
                }

                foreach (var providedService in hostInfo.HostSingletonServices)
                {
                    var service = new ScopeServiceDescriptor(
                        ImplementType: hostType,
                        ExposedServiceTypes: providedService.ExposedServiceTypes,
                        Lifetime: ServiceLifetime.Singleton,
                        IsHostProvided: true
                    );
                    builder.Add(service);
                }
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<ScopeServiceDescriptor> CollectScopeModulesAuto(ClassTypeInfo scopeInfo)
    {
        var builder = ImmutableArray.CreateBuilder<ScopeServiceDescriptor>();
        return builder.ToImmutable();
    }

    // ============================================================
    // 阶段 3: 图级别验证
    // ============================================================

    private void ValidateServiceGraph(DiGraph graph)
    {
        // ValidateDuplicateServiceRegistrations(graph);
        // ValidateLifetimeRules(graph);
        // ValidateConstructorDependencies(graph);
        // ValidateCircularDependencies(graph);
        // ValidateScopeDependencies(graph);
        // ValidateUnusedServices(graph);
    }
}
