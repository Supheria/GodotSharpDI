using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Coding;

/// <summary>
/// Scope 代码生成器
/// </summary>
internal static class ScopeGenerator
{
    public static void Generate(SourceProductionContext context, ScopeNode node, DiGraph graph)
    {
        var type = node.TypeInfo;
        var namespaceName = type.Symbol.ContainingNamespace.ToDisplayString();
        var className = type.Symbol.Name;

        ScopeLifecycleGenerator.GenerateLifecycle(context, node, graph, namespaceName, className);

        ScopeInterfaceGenerator.GenerateInterface(context, node, graph, namespaceName, className);
    }
}
