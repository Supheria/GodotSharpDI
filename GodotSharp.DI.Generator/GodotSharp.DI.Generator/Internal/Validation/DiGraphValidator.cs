using System.Collections.Generic;
using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal static class DiGraphValidator
{
    public static ImmutableArray<Diagnostic> Validate(
        IReadOnlyDictionary<INamedTypeSymbol, ClassTypeInfo> typeInfoMap,
        DiGraph graph,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        // diagnostics.AddRange(DuplicateServiceValidator.Validate(typeInfoMap, graph));
        // diagnostics.AddRange(LifetimeValidator.Validate(typeInfoMap, graph));
        // diagnostics.AddRange(ConstructorDependencyValidator.Validate(typeInfoMap, graph));
        // diagnostics.AddRange(UserInjectDependencyValidator.Validate(typeInfoMap, graph));
        // diagnostics.AddRange(CircularDependencyValidator.Validate(typeInfoMap, graph));
        // diagnostics.AddRange(ScopeModuleValidator.Validate(typeInfoMap, graph, symbols));
        // diagnostics.AddRange(UnusedServiceValidator.Validate(typeInfoMap, graph));
        return diagnostics.ToImmutable();
    }
}
