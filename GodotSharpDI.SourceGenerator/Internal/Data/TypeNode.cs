using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Type node
/// </summary>
internal sealed record TypeNode(
    ValidatedTypeInfo ValidatedTypeInfo,
    ImmutableArray<DependencyEdge> Dependencies,
    ImmutableArray<INamedTypeSymbol> ProvidedServices,
    // Mapping from service exposed type to implementation type
    // Key: Exposed type (e.g., interface IService)
    // Value: Implementation type (e.g., concrete class ServiceImpl)
    ImmutableDictionary<INamedTypeSymbol, INamedTypeSymbol> ServiceImplementationMap
);
