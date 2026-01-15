using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed record TypeInfo
{
    // ============================================================
    // 基础信息
    // ============================================================
    public INamedTypeSymbol Symbol { get; }
    public string Namespace { get; }

    // ============================================================
    // 角色标记（互斥）
    // ============================================================
    public bool IsService { get; }
    public bool IsUser { get; }
    public bool IsHost { get; }
    public bool IsScope { get; }

    // Node 类型（Host/User/Scope 可能是 Node）
    public bool IsNode { get; }

    // 是否实现 IServicesReady
    public bool IsServicesReady { get; }

    // ============================================================
    // Service 信息
    // ============================================================
    public ServiceLifetime Lifetime { get; }
    public ImmutableArray<ITypeSymbol> ServiceTypes { get; }
    public InjectConstructorDescriptor? Constructor { get; }

    // ============================================================
    // Host 信息
    // ============================================================
    public ImmutableArray<ProvidedServiceDescriptor> ProvidedServices { get; }

    // ============================================================
    // User 信息
    // ============================================================
    public ImmutableArray<InjectParameterDescriptor> InjectedMembers { get; }

    // ============================================================
    // Scope 信息
    // ============================================================
    public ImmutableArray<INamedTypeSymbol> ScopeInstantiate { get; }
    public ImmutableArray<INamedTypeSymbol> ScopeExpect { get; }

    // ============================================================
    // 构造函数（唯一入口）
    // ============================================================
    public TypeInfo(
        INamedTypeSymbol symbol,
        string? ns = null,
        bool isService = false,
        bool isUser = false,
        bool isHost = false,
        bool isScope = false,
        bool isNode = false,
        bool isServicesReady = false,
        ServiceLifetime lifetime = ServiceLifetime.None,
        ImmutableArray<ITypeSymbol>? serviceTypes = null,
        InjectConstructorDescriptor? constructor = null,
        ImmutableArray<ProvidedServiceDescriptor>? providedServices = null,
        ImmutableArray<InjectParameterDescriptor>? injectedMembers = null,
        ImmutableArray<INamedTypeSymbol>? scopeInstantiate = null,
        ImmutableArray<INamedTypeSymbol>? scopeExpect = null
    )
    {
        Symbol = symbol;
        Namespace = ns ?? symbol.ContainingNamespace.ToDisplayString();

        IsService = isService;
        IsUser = isUser;
        IsHost = isHost;
        IsScope = isScope;

        IsNode = isNode;
        IsServicesReady = isServicesReady;

        Lifetime = lifetime;

        ServiceTypes = serviceTypes ?? ImmutableArray<ITypeSymbol>.Empty;
        Constructor = constructor;

        ProvidedServices = providedServices ?? ImmutableArray<ProvidedServiceDescriptor>.Empty;
        InjectedMembers = injectedMembers ?? ImmutableArray<InjectParameterDescriptor>.Empty;

        ScopeInstantiate = scopeInstantiate ?? ImmutableArray<INamedTypeSymbol>.Empty;
        ScopeExpect = scopeExpect ?? ImmutableArray<INamedTypeSymbol>.Empty;
    }
}
