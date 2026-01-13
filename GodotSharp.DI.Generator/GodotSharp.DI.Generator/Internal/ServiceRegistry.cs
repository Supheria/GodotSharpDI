using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class ServiceRegistry
{
    public Dictionary<INamedTypeSymbol, ServiceTypeInfo> Services { get; } =
        new(SymbolEqualityComparer.Default);

    public Dictionary<INamedTypeSymbol, HostServiceInfo> Hosts { get; } =
        new(SymbolEqualityComparer.Default);

    public Dictionary<INamedTypeSymbol, UserDependencyInfo> Users { get; } =
        new(SymbolEqualityComparer.Default);

    public Dictionary<INamedTypeSymbol, ScopeServiceInfo> Scopes { get; } =
        new(SymbolEqualityComparer.Default);
}
