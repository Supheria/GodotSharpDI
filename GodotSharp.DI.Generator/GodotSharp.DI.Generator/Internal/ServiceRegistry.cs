using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class ServiceRegistry
{
    public Dictionary<INamedTypeSymbol, ServiceTypeInfo> Services { get; } =
        new(SymbolEqualityComparer.Default);

    public Dictionary<INamedTypeSymbol, HostDescriptor> Hosts { get; } =
        new(SymbolEqualityComparer.Default);

    public Dictionary<INamedTypeSymbol, UserDescriptor> Users { get; } =
        new(SymbolEqualityComparer.Default);

    public Dictionary<INamedTypeSymbol, ScopeDescriptor> Scopes { get; } =
        new(SymbolEqualityComparer.Default);
}
