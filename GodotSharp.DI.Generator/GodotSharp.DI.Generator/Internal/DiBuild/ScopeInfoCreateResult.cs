using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal sealed record ScopeInfoCreateResult(
    ScopeInfo? ScopeInfo,
    ImmutableArray<Diagnostic> Diagnostics
);
