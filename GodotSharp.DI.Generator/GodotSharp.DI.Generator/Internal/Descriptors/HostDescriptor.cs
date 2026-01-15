using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal sealed record HostDescriptor(
    INamedTypeSymbol Type,
    IReadOnlyList<ProvidedServiceDescriptor> ProvidedServices,
    IReadOnlyList<InjectParameterDescriptor> InjectedMembers
);
