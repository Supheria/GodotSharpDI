using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Data;

/// <summary>
/// DI 图构建结果
/// </summary>
internal sealed record DiGraphBuildResult(DiGraph? Graph, ImmutableArray<Diagnostic> Diagnostics)
{
    public static DiGraphBuildResult Empty => new(null, ImmutableArray<Diagnostic>.Empty);
}
