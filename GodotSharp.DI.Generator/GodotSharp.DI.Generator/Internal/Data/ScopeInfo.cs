using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

internal sealed record ScopeInfo(
    INamedTypeSymbol Symbol,
    string Namespace,
    ImmutableArray<ScopeServiceDescriptor> Services
);
