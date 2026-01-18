using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

internal sealed record DiGraph(
    ImmutableArray<ServiceInfo> Services,
    ImmutableArray<HostUserInfo> HostOrUsers,
    ImmutableArray<ScopeInfo> Scopes
);
