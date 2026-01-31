using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

internal static class ClassPipeline
{
    public static ClassValidationResult ValidateAndClassify(
        RawClassSemanticInfo raw,
        CachedSymbols symbols
    )
    {
        var validator = new ClassValidator(raw, symbols);
        return validator.Validate();
    }
}
