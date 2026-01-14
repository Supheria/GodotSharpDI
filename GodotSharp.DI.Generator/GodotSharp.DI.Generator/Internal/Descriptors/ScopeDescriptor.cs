using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record ScopeDescriptor(
    INamedTypeSymbol Type,
    IReadOnlyList<INamedTypeSymbol> InstantiatedServices,
    IReadOnlyList<INamedTypeSymbol> ExpectedHosts
);
