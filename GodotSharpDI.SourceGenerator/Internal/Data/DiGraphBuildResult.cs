using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// DI graph build result
/// </summary>
internal sealed record DiGraphBuildResult(DiGraph? Graph, ImmutableArray<Diagnostic> Diagnostics)
{
    public static DiGraphBuildResult Empty => new(null, ImmutableArray<Diagnostic>.Empty);
}
