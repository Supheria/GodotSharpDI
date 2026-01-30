using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Semantic;

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
