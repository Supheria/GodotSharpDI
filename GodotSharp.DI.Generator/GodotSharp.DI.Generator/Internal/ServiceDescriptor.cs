using System;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class ServiceDescriptor
{
    public INamedTypeSymbol ServiceType { get; }
    public INamedTypeSymbol ImplementationType { get; }
    public ServiceLifetime Lifetime { get; }

    // 是否由 Host 节点提供（Host 的成员暴露）
    public bool ProvidedByHost { get; }

    // 是否由 Scope 显式 Instantiate
    public bool ProvidedByScope { get; }

    // 是否是“自类型暴露”（如 [SingletonService] class A : A）
    public bool IsImplicit { get; }

    public ServiceDescriptor(
        INamedTypeSymbol serviceType,
        INamedTypeSymbol implementationType,
        ServiceLifetime lifetime,
        bool providedByHost = false,
        bool providedByScope = false,
        bool isImplicit = false
    )
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
        ProvidedByHost = providedByHost;
        ProvidedByScope = providedByScope;
        IsImplicit = isImplicit;
    }
}
