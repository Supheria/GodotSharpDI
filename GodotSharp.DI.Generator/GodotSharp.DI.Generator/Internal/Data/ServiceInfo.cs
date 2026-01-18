using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.Data;

internal sealed record ServiceInfo(
    INamedTypeSymbol Symbol,
    string Namespace,
    ServiceLifetime Lifetime,
    InjectConstructorDescriptor Constructor
);
