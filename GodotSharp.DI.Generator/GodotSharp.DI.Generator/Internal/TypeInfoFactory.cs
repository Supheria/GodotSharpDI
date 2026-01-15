using System;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class TypeInfoFactory
{
    // ============================================================
    // Service
    // ============================================================
    public static TypeInfo CreateService(
        INamedTypeSymbol type,
        SymbolCache symbolCache,
        ServiceLifetime lifetime
    )
    {
        var ctor = TypeInfoCollectors.CollectInjectConstructor(type, symbolCache);
        var serviceTypes = TypeInfoCollectors.CollectServiceTypes(type, symbolCache);

        return new TypeInfo(type)
        {
            IsService = true,
            Lifetime = lifetime,
            ServiceTypes = serviceTypes,
            Constructor = ctor,
            IsServicesReady = TypeHelper.IsServicesReady(type, symbolCache.ServicesReadyInterface),
        };
    }

    // ============================================================
    // Host
    // ============================================================
    public static TypeInfo CreateHost(INamedTypeSymbol type, SymbolCache symbolCache, bool isNode)
    {
        var provided = TypeInfoCollectors.CollectProvidedServices(type, symbolCache);

        return new TypeInfo(type)
        {
            IsNode = isNode,
            IsHost = true,
            ProvidedServices = provided,
            IsServicesReady = TypeHelper.IsServicesReady(type, symbolCache.ServicesReadyInterface),
        };
    }

    // ============================================================
    // User
    // ============================================================
    public static TypeInfo CreateUser(INamedTypeSymbol type, SymbolCache symbolCache, bool isNode)
    {
        var injected = TypeInfoCollectors.CollectInjectedMembers(type, symbolCache);

        return new TypeInfo(type)
        {
            IsNode = isNode,
            IsUser = true,
            InjectedMembers = injected,
            IsServicesReady = TypeHelper.IsServicesReady(type, symbolCache.ServicesReadyInterface),
        };
    }

    // ============================================================
    // Scope
    // ============================================================
    public static TypeInfo CreateScope(INamedTypeSymbol type, SymbolCache symbolCache)
    {
        var modules = TypeHelper.GetAttribute(type, symbolCache.ModulesAttribute);
        var auto = TypeHelper.GetAttribute(type, symbolCache.AutoModulesAttribute);

        var instantiate = TypeInfoCollectors.CollectInstantiate(type, modules, auto);
        var expect = TypeInfoCollectors.CollectExpect(type, modules, auto);

        return new TypeInfo(type)
        {
            IsScope = true,
            ScopeInstantiate = instantiate,
            ScopeExpect = expect,
            IsServicesReady = TypeHelper.IsServicesReady(type, symbolCache.ServicesReadyInterface),
        };
    }
}
