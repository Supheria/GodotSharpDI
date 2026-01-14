using System.Collections.Generic;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed record TypeInfo
{
    public INamedTypeSymbol Symbol { get; init; }
    public string Namespace { get; init; }

    // Roles
    public bool IsHost { get; init; }
    public bool IsUser { get; init; }
    public bool IsService { get; init; }
    public bool IsScope { get; init; }
    public bool IsNode { get; init; }
    public bool IsServicesReady { get; init; }

    // Service lifetime
    public ServiceLifetime? Lifetime { get; init; } // Singleton / Transient / null

    // Service info
    public IReadOnlyList<ITypeSymbol> ServiceTypes { get; init; }
    public InjectConstructorDescriptor? Constructor { get; init; }

    // Host info
    public IReadOnlyList<ProvidedServiceDescriptor> ProvidedServices { get; init; }

    // User info
    public IReadOnlyList<InjectedMemberDescriptor> InjectedMembers { get; init; }

    // Scope info
    public IReadOnlyList<INamedTypeSymbol> ScopeInstantiate { get; init; }
    public IReadOnlyList<INamedTypeSymbol> ScopeExpect { get; init; }
}
