using System;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class TypeInfoFactory
{
    public static TypeInfo FromService(
        INamedTypeSymbol type,
        ServiceLifetime lifetime,
        SymbolCache symbols
    )
    {
        var serviceTypes = type.ExtractServiceTypes(symbols);
        var ctor = type.ExtractInjectConstructor(symbols);
        var ns = type.ContainingNamespace.ToDisplayString();
        var isNode = SymbolHelper.IsGodotNode(type, symbols.GodotNodeType);

        return new TypeInfo
        {
            Symbol = type,
            Namespace = ns,
            IsService = true,
            IsHost = false,
            IsUser = false,
            IsScope = false,
            IsNode = isNode,
            IsServicesReady = SymbolHelper.IsServicesReady(type, symbols.ServicesReadyInterface),
            Lifetime = lifetime,
            ServiceTypes = serviceTypes,
            Constructor = ctor,
            ProvidedServices = Array.Empty<ProvidedServiceDescriptor>(),
            InjectedMembers = Array.Empty<InjectedMemberDescriptor>(),
            ScopeInstantiate = Array.Empty<INamedTypeSymbol>(),
            ScopeExpect = Array.Empty<INamedTypeSymbol>(),
        };
    }

    public static TypeInfo FromHost(INamedTypeSymbol type, SymbolCache symbols)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        var isNode = SymbolHelper.IsGodotNode(type, symbols.GodotNodeType);
        var provided = type.ExtractProvidedServices(symbols);
        var injected = type.ExtractInjectedMembers(symbols);

        return new TypeInfo
        {
            Symbol = type,
            Namespace = ns,
            IsService = false,
            IsHost = true,
            IsUser = false,
            IsScope = false,
            IsNode = isNode,
            IsServicesReady = SymbolHelper.IsServicesReady(type, symbols.ServicesReadyInterface),
            Lifetime = null,
            ServiceTypes = Array.Empty<ITypeSymbol>(),
            Constructor = null,
            ProvidedServices = provided,
            InjectedMembers = injected,
            ScopeInstantiate = Array.Empty<INamedTypeSymbol>(),
            ScopeExpect = Array.Empty<INamedTypeSymbol>(),
        };
    }

    public static TypeInfo FromUser(INamedTypeSymbol type, SymbolCache symbols)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        var isNode = SymbolHelper.IsGodotNode(type, symbols.GodotNodeType);
        var injected = type.ExtractInjectedMembers(symbols);

        return new TypeInfo
        {
            Symbol = type,
            Namespace = ns,
            IsService = false,
            IsHost = false,
            IsUser = true,
            IsScope = false,
            IsNode = isNode,
            IsServicesReady = SymbolHelper.IsServicesReady(type, symbols.ServicesReadyInterface),
            Lifetime = null,
            ServiceTypes = Array.Empty<ITypeSymbol>(),
            Constructor = null,
            ProvidedServices = Array.Empty<ProvidedServiceDescriptor>(),
            InjectedMembers = injected,
            ScopeInstantiate = Array.Empty<INamedTypeSymbol>(),
            ScopeExpect = Array.Empty<INamedTypeSymbol>(),
        };
    }

    public static TypeInfo FromScope(INamedTypeSymbol type, SymbolCache symbols)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        var isNode = SymbolHelper.IsGodotNode(type, symbols.GodotNodeType);
        var modules = SymbolHelper.GetAttribute(type, symbols.ModulesAttribute);
        var auto = SymbolHelper.GetAttribute(type, symbols.AutoModulesAttribute);

        var instantiate = type.ExtractInstantiate(modules, auto);
        var expect = type.ExtractExpect(modules, auto);

        return new TypeInfo
        {
            Symbol = type,
            Namespace = ns,
            IsService = false,
            IsHost = false,
            IsUser = false,
            IsScope = true,
            IsNode = isNode,
            IsServicesReady = SymbolHelper.IsServicesReady(type, symbols.ServicesReadyInterface),
            Lifetime = null,
            ServiceTypes = Array.Empty<ITypeSymbol>(),
            Constructor = null,
            ProvidedServices = Array.Empty<ProvidedServiceDescriptor>(),
            InjectedMembers = Array.Empty<InjectedMemberDescriptor>(),
            ScopeInstantiate = instantiate,
            ScopeExpect = expect,
        };
    }
}
