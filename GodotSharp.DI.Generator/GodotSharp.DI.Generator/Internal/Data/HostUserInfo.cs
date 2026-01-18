using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

internal sealed record HostUserInfo(
    INamedTypeSymbol Symbol,
    string Namespace,
    bool IsHost,
    bool IsUser,
    bool IsServicesReady,
    bool IsNode,
    ImmutableArray<ProvidedServiceDescriptor> ProvidedServices,
    ImmutableArray<InjectTypeDescriptor> InjectedMembers
);
