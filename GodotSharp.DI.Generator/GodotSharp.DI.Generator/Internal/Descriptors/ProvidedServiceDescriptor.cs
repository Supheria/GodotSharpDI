using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record ProvidedServiceDescriptor(
    ImmutableArray<ITypeSymbol> ExposedServiceTypes,
    string Name
);
