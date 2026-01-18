using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record ScopeServiceDescriptor(
    INamedTypeSymbol ImplementType, // DatabaseWriter / PathFinder / HostType
    ImmutableArray<ITypeSymbol> ExposedServiceTypes, // IDataWriter, IDataReader, ICellGetter...
    ServiceLifetime Lifetime, // Singleton / Transient
    bool IsHostProvided // true = Host.Expect；false = Scope.Instantiate / Transient
);
