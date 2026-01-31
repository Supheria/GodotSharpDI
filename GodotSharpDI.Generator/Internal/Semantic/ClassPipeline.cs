using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Generator.Shared;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharpDI.Generator.Internal.Data.TypeInfo;

namespace GodotSharpDI.Generator.Internal.Semantic;

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
