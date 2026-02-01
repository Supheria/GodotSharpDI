using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;

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
