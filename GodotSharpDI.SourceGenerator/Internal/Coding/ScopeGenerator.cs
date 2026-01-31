using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

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
