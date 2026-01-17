using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal sealed record ClassTypeInfoBuildResult(
    ClassTypeInfo? TypeInfo,
    ImmutableArray<Diagnostic> Diagnostics
)
{
    public static ClassTypeInfoBuildResult Failure(ImmutableArray<Diagnostic> diagnostics)
    {
        return new ClassTypeInfoBuildResult(null, diagnostics);
    }
}
