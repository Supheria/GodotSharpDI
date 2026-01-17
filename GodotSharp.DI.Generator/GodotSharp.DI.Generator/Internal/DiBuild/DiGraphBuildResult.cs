using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal sealed record DiGraphBuildResult(DiGraph? Graph, ImmutableArray<Diagnostic> Diagnostics)
{
    public static DiGraphBuildResult Failure(ImmutableArray<Diagnostic> diagnostics)
    {
        return new DiGraphBuildResult(null, diagnostics);
    }
}
