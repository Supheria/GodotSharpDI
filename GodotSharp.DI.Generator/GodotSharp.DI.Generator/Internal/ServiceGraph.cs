using System.Collections.Generic;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed record ServiceGraph(
    IReadOnlyList<TypeInfo> Services,
    IReadOnlyList<TypeInfo> Hosts,
    IReadOnlyList<TypeInfo> Users,
    IReadOnlyList<TypeInfo> Scopes
)
{
    // 方便做静态依赖分析：服务类型 → 实现
    public Dictionary<ISymbol?, TypeInfo> ServiceTypeMap { get; } =
        Services
            .SelectMany(s => s.ServiceTypes.Select(st => (st, s)))
            .ToDictionary(x => x.st, x => x.s, SymbolEqualityComparer.Default);
}
