using GodotSharpDI.SourceGenerator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// 源代码生成器统一入口
/// </summary>
internal static class SourceEmitter
{
    /// <summary>
    /// 生成所有代码
    /// </summary>
    public static void GenerateAll(SourceProductionContext context, DiGraph graph)
    {
        // 生成 Service 工厂
        foreach (var node in graph.ServiceNodes)
        {
            ServiceGenerator.Generate(context, node);
        }

        // 生成 Host 代码
        foreach (var node in graph.HostNodes)
        {
            HostGenerator.Generate(context, node);
        }

        // 生成 User 代码
        foreach (var node in graph.UserNodes)
        {
            UserGenerator.Generate(context, node);
        }

        // 生成 HostAndUser 代码
        foreach (var node in graph.HostAndUserNodes)
        {
            HostAndUserGenerator.Generate(context, node);
        }

        // 生成 Scope 代码
        foreach (var node in graph.ScopeNodes)
        {
            ScopeGenerator.Generate(context, node, graph);
        }
    }
}
