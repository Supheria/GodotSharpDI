using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record ServiceDescriptor(
    INamedTypeSymbol Implementation,
    ServiceLifetime Lifetime,
    IReadOnlyList<ITypeSymbol> ServiceTypes,
    InjectConstructorDescriptor? Constructor
);
