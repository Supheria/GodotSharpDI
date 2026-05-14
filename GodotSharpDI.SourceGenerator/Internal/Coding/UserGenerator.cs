using System.Collections.Immutable;
using System.Linq;
using System.Text;
using GodotSharpDI.SourceGenerator.Internal.Coding.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// User code generator
/// </summary>
internal static class UserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // Generate base DI file
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // Generate dependency injection code
        InjectionGenerator.Generate(context, node);
    }
}
