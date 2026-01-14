using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record InjectConstructorDescriptor(
    IMethodSymbol Constructor,
    IReadOnlyList<ITypeSymbol> ParameterTypes
);
