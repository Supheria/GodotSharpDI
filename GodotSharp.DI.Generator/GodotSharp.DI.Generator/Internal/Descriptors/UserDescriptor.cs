using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record UserDescriptor(
    INamedTypeSymbol Type,
    IReadOnlyList<InjectParameterDescriptor> InjectedMembers
);
