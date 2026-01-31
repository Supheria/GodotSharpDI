using System.Linq;
using GodotSharpDI.Generator.Internal.Helpers;
using GodotSharpDI.Generator.Shared;
using GodotSharpDI.Generator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Coding;

/// <summary>
/// Scope 代码生成器
/// </summary>
internal static class ScopeGenerator
{
    public static void Generate(SourceProductionContext context, ScopeNode node, DiGraph graph)
    {
        ScopeLifecycleGenerator.GenerateLifecycle(context, node, graph);

        ScopeInterfaceGenerator.GenerateInterface(context, node, graph);
    }
}
