using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record ScopeServiceDescriptor(
    INamedTypeSymbol Implementation, // DatabaseWriter / PathFinder / HostType
    ImmutableArray<ITypeSymbol> ServiceTypes, // IDataWriter, IDataReader, ICellGetter...
    ServiceLifetime Lifetime, // Singleton / Transient
    bool IsHostProvided // true = Host.Expect；false = Scope.Instantiate / Transient
);
