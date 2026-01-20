using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal sealed class DiGraphBuilder
{
    private readonly ImmutableArray<ClassType> _allTypes;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public DiGraphBuilder(ImmutableArray<ClassType> allTypes, CachedSymbols symbols)
    {
        _allTypes = allTypes;
        _symbols = symbols;
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    }

    public DiGraphBuildResult Build()
    {
        var typeInfoMap = BuildTypeInfoMap();
        var graph = BuildDiGraph(typeInfoMap);
        _diagnostics.AddRange(DiGraphValidator.Validate(typeInfoMap, graph, _symbols));
        return new DiGraphBuildResult(graph, _diagnostics.ToImmutable());
    }

    private IReadOnlyDictionary<INamedTypeSymbol, ClassTypeInfo> BuildTypeInfoMap()
    {
        var typeInfoMap = new Dictionary<INamedTypeSymbol, ClassTypeInfo>(
            SymbolEqualityComparer.Default
        );
        foreach (var type in _allTypes)
        {
            var result = ClassTypeInfoFactory.Create(type, _symbols);
            _diagnostics.AddRange(result.Diagnostics);
            if (result.TypeInfo is not null)
            {
                typeInfoMap.Add(result.TypeInfo.Symbol, result.TypeInfo);
            }
        }
        return typeInfoMap;
    }

    private DiGraph BuildDiGraph(IReadOnlyDictionary<INamedTypeSymbol, ClassTypeInfo> typeInfoMap)
    {
        var services = ImmutableArray.CreateBuilder<ServiceInfo>();
        var hostOrUsers = ImmutableArray.CreateBuilder<HostUserInfo>();
        var scopes = ImmutableArray.CreateBuilder<ScopeInfo>();

        foreach (var typeInfo in typeInfoMap.Values)
        {
            if (typeInfo.IsService)
            {
                services.Add(
                    new ServiceInfo(
                        typeInfo.Symbol,
                        typeInfo.Namespace,
                        typeInfo.ServiceLifetime,
                        typeInfo.ServiceConstructor!
                    )
                );
            }
            else if (typeInfo.IsHost || typeInfo.IsUser)
            {
                hostOrUsers.Add(
                    new HostUserInfo(
                        typeInfo.Symbol,
                        typeInfo.Namespace,
                        typeInfo.IsHost,
                        typeInfo.IsUser,
                        typeInfo.IsServicesReady,
                        typeInfo.IsNode,
                        typeInfo.HostSingletonServices,
                        typeInfo.UserInjectMembers
                    )
                );
            }
            else if (typeInfo.IsScope)
            {
                var result = ScopeInfoCreator.Create(typeInfo, typeInfoMap);
                _diagnostics.AddRange(result.Diagnostics);
                if (result.ScopeInfo is not null)
                {
                    scopes.Add(result.ScopeInfo);
                }
            }
        }

        return new DiGraph(
            Services: services.ToImmutable(),
            HostOrUsers: hostOrUsers.ToImmutable(),
            Scopes: scopes.ToImmutable()
        );
    }
}
