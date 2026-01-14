using System.Collections.Generic;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed record ServiceGraph(
    IReadOnlyList<TypeInfo> Services,
    IReadOnlyList<TypeInfo> Hosts,
    IReadOnlyList<TypeInfo> Users,
    IReadOnlyList<TypeInfo> Scopes
);
