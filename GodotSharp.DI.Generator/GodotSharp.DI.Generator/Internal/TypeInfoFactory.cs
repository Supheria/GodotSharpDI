using System;
using System.Collections.Immutable;
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
        SymbolCache symbols,
        ServiceLifetime lifetime
    )
    {
        var serviceTypes = TypeInfoCollectors.CollectServiceTypes(type, symbols, lifetime);
        var ctor = TypeInfoCollectors.CollectInjectConstructor(type, symbols);

        return new TypeInfo(
            symbol: type,
            isService: true,
            lifetime: lifetime,
            serviceTypes: serviceTypes,
            constructor: ctor,
            isServicesReady: TypeHelper.IsServicesReady(type, symbols.ServicesReadyInterface)
        );
    }

    // ============================================================
    // Host
    // ============================================================
    public static TypeInfo CreateHost(INamedTypeSymbol type, SymbolCache symbols, bool isNode)
    {
        var provided = TypeInfoCollectors.CollectProvidedServices(type, symbols);

        return new TypeInfo(
            symbol: type,
            isHost: true,
            isNode: isNode,
            providedServices: provided,
            isServicesReady: TypeHelper.IsServicesReady(type, symbols.ServicesReadyInterface)
        );
    }

    // ============================================================
    // User
    // ============================================================
    public static TypeInfo CreateUser(INamedTypeSymbol type, SymbolCache symbols, bool isNode)
    {
        var injected = TypeInfoCollectors.CollectInjectedMembers(type, symbols);

        return new TypeInfo(
            symbol: type,
            isUser: true,
            isNode: isNode,
            injectedMembers: injected,
            isServicesReady: TypeHelper.IsServicesReady(type, symbols.ServicesReadyInterface)
        );
    }

    // ============================================================
    // Scope
    // ============================================================
    public static TypeInfo CreateScope(INamedTypeSymbol type, SymbolCache symbols)
    {
        var modules = TypeHelper.GetAttribute(type, symbols.ModulesAttribute);
        var auto = TypeHelper.GetAttribute(type, symbols.AutoModulesAttribute);

        var instantiate = TypeInfoCollectors.CollectInstantiate(type, modules, auto);
        var expect = TypeInfoCollectors.CollectExpect(type, modules, auto);

        return new TypeInfo(
            symbol: type,
            isScope: true,
            scopeInstantiate: instantiate,
            scopeExpect: expect,
            isServicesReady: TypeHelper.IsServicesReady(type, symbols.ServicesReadyInterface)
        );
    }
}
