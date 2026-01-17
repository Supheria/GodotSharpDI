using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record InjectConstructorDescriptor(
    IMethodSymbol Constructor,
    ImmutableArray<InjectTypeDescriptor> Parameters
);
